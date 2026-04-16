using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

                if (!context.Request.Headers.TryGetValue("X-Jwt-Token", out var hdr))
                    return Task.CompletedTask;

                var raw = hdr.ToString().Trim();
                if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    raw = raw["Bearer ".Length..].Trim();
                if (raw.Length > 0)
                    context.Token = raw;

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var body = JsonSerializer.Serialize(new
                {
                    message = "Thiếu JWT. Dùng Authorization: Bearer <token> hoặc header X-Jwt-Token: <token> (từ POST /api/auth/login)."
                });
                await context.Response.WriteAsync(body);
            },
            OnAuthenticationFailed = async context =>
            {
                context.NoResult();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var message = context.Exception is SecurityTokenExpiredException
                    ? "Token đã hết hạn. Đăng nhập lại để lấy JWT mới."
                    : "Token không hợp lệ hoặc không đọc được.";
                var body = JsonSerializer.Serialize(new { message });
                await context.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
