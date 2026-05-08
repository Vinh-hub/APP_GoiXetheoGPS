using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using RideAPI.Middleware;
using RideAPI.Services;
using RideAPI.Swagger;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT từ POST /api/auth/login. Swagger tự thêm Bearer; không gõ thêm chữ Bearer.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityDefinition("XJwtToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Jwt-Token",
        Description = "Dán nguyên chuỗi JWT từ login (không cần Bearer)."
    });
    options.DocumentFilter<BearerDocumentFilter>();
    options.OperationFilter<AnonymousSecurityOperationFilter>();
    options.OperationFilter<DualJwtSecurityOperationFilter>();
    options.OperationFilter<JwtInHeaderParameterOperationFilter>();
});

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<PasswordHasherService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RefreshTokenService>();
builder.Services.AddSingleton<AuthSchemaService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<DbRetryService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient();

var jwtKey = GetJwtKey(builder.Configuration, builder.Environment);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RideAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RideApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrEmpty(context.Token))
                    return Task.CompletedTask;

                if (context.Request.Cookies.TryGetValue("admin_jwt", out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken.Trim();
                    return Task.CompletedTask;
                }

                if (!context.Request.Headers.TryGetValue("X-Jwt-Token", out var hdr))
                    return Task.CompletedTask;

                var raw = hdr.ToString().Trim();
                if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    raw = raw["Bearer ".Length..].Trim();
                if (raw.Length > 0)
                    context.Token = raw;

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var regionIdRaw = context.Principal?.FindFirst("regionId")?.Value;
                var region = int.TryParse(regionIdRaw, out var regionId) && regionId == 1 ? "NORTH" : "SOUTH";
                var jwtId = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                var refreshTokens = context.HttpContext.RequestServices.GetRequiredService<RefreshTokenService>();
                if (await refreshTokens.IsJwtRevokedAsync(region, jwtId))
                    context.Fail("Token has been revoked.");
            },
            OnAuthenticationFailed = context =>
            {
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                    return;

                if (context.Request.Path.StartsWithSegments("/admin"))
                {
                    context.Response.Redirect("/admin/login");
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";

                var message = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => "Token đã hết hạn. Đăng nhập lại để lấy JWT mới.",
                    not null => "Token không hợp lệ hoặc không đọc được.",
                    _ => "Thiếu JWT. Dùng Authorization: Bearer <token> hoặc header X-Jwt-Token: <token>."
                };

                var body = JsonSerializer.Serialize(new { message });
                await context.Response.WriteAsync(body);
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                    return;

                if (context.Request.Path.StartsWithSegments("/admin"))
                {
                    context.Response.Redirect("/admin/unauthorized");
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Bạn không có quyền truy cập tài nguyên này." }));
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

try
{
    await EnsureAuthSchemaAsync(app.Services);
    await EnsureAdminAccountAsync(app.Services, builder.Configuration);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("DB_NODES_DOWN") || ex.Message.Contains("DOWN_CANNOT_WRITE"))
{
    Console.WriteLine($"⚠️  Warning: Could not seed admin accounts - {ex.Message}. Database may not be available. Application will continue without admin seed data.");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Warning: Unexpected error during admin account seeding: {ex.Message}");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/admin/status/500");
}

app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    if (http.Response.HasStarted)
        return;

    if (http.Request.Path.StartsWithSegments("/api"))
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            message = http.Response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Bạn cần đăng nhập.",
                StatusCodes.Status403Forbidden => "Bạn không có quyền truy cập.",
                StatusCodes.Status404NotFound => "Không tìm thấy API.",
                _ => "Yêu cầu không thành công."
            }
        }));
        return;
    }

    http.Response.Redirect($"/admin/status/{http.Response.StatusCode}");
});

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseMiddleware<LocationRoutingMiddleware>();
app.MapGet("/", () => Results.Redirect("/admin/login"));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();

static string GetJwtKey(IConfiguration configuration, IWebHostEnvironment environment)
{
    var jwtKey = configuration["Jwt:Key"];
    if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length >= 32)
        return jwtKey;

    if (environment.IsDevelopment())
    {
        Console.WriteLine("Warning: Jwt:Key is missing; using a development-only signing key.");
        return "DevelopmentOnlyJwtSigningKey_ChangeMe_AtLeast32Chars";
    }

    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");
}

static async Task EnsureAuthSchemaAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var schema = scope.ServiceProvider.GetRequiredService<AuthSchemaService>();
    await schema.EnsureAsync();
}

static async Task EnsureAdminAccountAsync(IServiceProvider services, IConfiguration configuration)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    var adminEmail = configuration["AdminSeed:Email"] ?? "admin@rideapi.local";
    var adminPassword = configuration["AdminSeed:Password"] ?? "Admin@123";
    var adminName = configuration["AdminSeed:Name"] ?? "System Admin";
    var adminPhone = configuration["AdminSeed:Phone"] ?? "0900000000";

    await EnsureAdminForRegionAsync(await db.GetConnectionAsync("NORTH", true), 1, adminEmail, adminPassword, adminName, adminPhone);
    await EnsureAdminForRegionAsync(await db.GetConnectionAsync("SOUTH", true), 2, adminEmail, adminPassword, adminName, adminPhone);
}

static async Task EnsureAdminForRegionAsync(NpgsqlConnection conn, int regionId, string email, string password, string name, string phone)
{
    await using (conn)
    {
        const string upsertAdminSql = @"
INSERT INTO Users (Email, Password, Role, CustomerID, DriverID, Name, Phone, RegionID, IsActive)
VALUES (@email, @password, 'Admin', NULL, NULL, @name, @phone, @regionId, TRUE)
ON CONFLICT (Email)
DO UPDATE SET
    Password = EXCLUDED.Password,
    Role = 'Admin',
    CustomerID = NULL,
    DriverID = NULL,
    Name = EXCLUDED.Name,
    Phone = EXCLUDED.Phone,
    RegionID = EXCLUDED.RegionID,
    IsActive = TRUE;";

        await using var cmd = new NpgsqlCommand(upsertAdminSql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@password", password);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@phone", phone);
        cmd.Parameters.AddWithValue("@regionId", regionId);
        await cmd.ExecuteNonQueryAsync();
    }
}
