using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using RideAPI.Services;
using RideAPI.Swagger;
using RideAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);
// Add services
//builder.Services.AddControllers();
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
    // ApiKey = ô nhập token ngay khi bấm ổ khóa từng API (không phụ thuộc nút Authorize góc trên).
    options.AddSecurityDefinition("XJwtToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Jwt-Token",
        Description = "Dán nguyên chuỗi JWT từ login (không cần Bearer). Mỗi API có ô riêng khi bấm khóa."
    });
    // Bearer toàn tài liệu: giữ nút Authorize góc trên + fallback khi operation không ghi đè security.
    options.DocumentFilter<BearerDocumentFilter>();
    options.OperationFilter<AnonymousSecurityOperationFilter>();
    // API cần auth: Bearer HOẶC X-Jwt-Token (Swagger hiện cả hai lựa chọn trên từng endpoint).
    options.OperationFilter<DualJwtSecurityOperationFilter>();
    // Thêm luôn dòng header trong Parameters (Try it out) cho dễ thấy.
    options.OperationFilter<JwtInHeaderParameterOperationFilter>();
});

// App services
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


// JWT: phải trùng Jwt:* trong appsettings.json với AuthController (ký + kiểm token).
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RideAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RideApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Giữ claim JWT đúng tên trong token (role, driverId, …) — khớp AuthController + DriversController.
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

                if (context.Request.Cookies.TryGetValue("admin_jwt", out var cookieToken) &&
                    !string.IsNullOrWhiteSpace(cookieToken))
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
                // Do not write the response here. Let OnChallenge return one unified 401 response.
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
                    _ => "Thiếu JWT. Dùng Authorization: Bearer <token> hoặc header X-Jwt-Token: <token> (từ POST /api/auth/login)."
                };

                var body = JsonSerializer.Serialize(new { message });
                await context.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

await EnsureAdminAccountAsync(app.Services, builder.Configuration);

// Configure pipeline
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
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

    await EnsureAdminForRegionAsync(db.GetConnection(20), 1, adminEmail, adminPassword, adminName, adminPhone);
    await EnsureAdminForRegionAsync(db.GetConnection(10), 2, adminEmail, adminPassword, adminName, adminPhone);
}

static async Task EnsureAdminForRegionAsync(
    NpgsqlConnection conn,
    int regionId,
    string email,
    string password,
    string name,
    string phone)
{
    await using (conn)
    {
        await conn.OpenAsync();

        const string alterSql = @"
ALTER TABLE Users DROP CONSTRAINT IF EXISTS users_role_check;
ALTER TABLE Users DROP CONSTRAINT IF EXISTS users_check;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'users_role_check'
          AND conrelid = 'users'::regclass
    ) THEN
        ALTER TABLE Users
        ADD CONSTRAINT users_role_check
        CHECK (Role IN ('Customer', 'Driver', 'Admin'));
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'users_check'
          AND conrelid = 'users'::regclass
    ) THEN
        ALTER TABLE Users
        ADD CONSTRAINT users_check
        CHECK (
            (Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
            (Role='Driver'   AND DriverID   IS NOT NULL AND CustomerID IS NULL) OR
            (Role='Admin'    AND CustomerID IS NULL AND DriverID IS NULL)
        );
    END IF;
END $$;";

        await using (var alterCmd = new NpgsqlCommand(alterSql, conn))
        {
            await alterCmd.ExecuteNonQueryAsync();
        }

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
