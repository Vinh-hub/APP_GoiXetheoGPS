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

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
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
            ClockSkew = TimeSpan.FromMinutes(2)
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
            OnAuthenticationFailed = context =>
            {
                context.NoResult();
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
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

await EnsureAdminAccountAsync(app.Services, builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseStaticFiles();
app.UseMiddleware<LocationRoutingMiddleware>();
app.MapGet("/", () => Results.Redirect("/admin/login"));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();

static async Task EnsureAdminAccountAsync(IServiceProvider services, IConfiguration configuration)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

    var adminEmail = configuration["AdminSeed:Email"] ?? "admin@rideapi.local";
    var adminPassword = configuration["AdminSeed:Password"] ?? "Admin@123";
    var adminName = configuration["AdminSeed:Name"] ?? "System Admin";
    var adminPhone = configuration["AdminSeed:Phone"] ?? "0900000000";

    await EnsureAdminForRegionAsync(await db.GetConnectionAsync("NORTH", false), 1, adminEmail, adminPassword, adminName, adminPhone);
    await EnsureAdminForRegionAsync(await db.GetConnectionAsync("SOUTH", false), 2, adminEmail, adminPassword, adminName, adminPhone);
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
