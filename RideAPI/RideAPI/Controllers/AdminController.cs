using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using RideAPI.Models.ViewModels;
using RideAPI.Services;

namespace RideAPI.Controllers;

[Authorize]
[Route("admin")]
public class AdminController : Controller
{
    private readonly DatabaseService _db;
    private readonly IConfiguration _config;

    public AdminController(DatabaseService db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var admin = await FindAdminAsync(model.Email.Trim(), model.Password);
        if (admin is null)
        {
            ModelState.AddModelError(string.Empty, "Sai email/mật khẩu hoặc tài khoản không có quyền Admin.");
            return View(model);
        }

        var token = GenerateAdminToken(admin.Value.UserId, admin.Value.Name, admin.Value.Email, admin.Value.RegionId);
        Response.Cookies.Append("admin_jwt", token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(1)
        });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("admin_jwt");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var fallbackRegionId = ParseRegionId(User.FindFirst("regionId")?.Value);
        var scopedRegionId = AdminRegionScopeHelper.GetScopedRegionId(Request, fallbackRegionId);

        var model = new AdminDashboardViewModel
        {
            Name = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin",
            Email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Role = role,
            RegionId = fallbackRegionId.ToString(),
            ScopedRegionId = scopedRegionId,
            ScopedRegionText = AdminRegionScopeHelper.GetScopeLabel(scopedRegionId),
            ScopeLatitude = AdminRegionScopeHelper.GetScopeLatitudeText(Request),
            ScopeProvince = AdminRegionScopeHelper.GetScopeProvince(Request),
            GeneratedAtUtc = DateTime.UtcNow
        };

        return View(model);
    }

    [HttpPost("dashboard/scope")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateScope(double? latitude, string? province)
    {
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var fallbackRegionId = ParseRegionId(User.FindFirst("regionId")?.Value);
        var scopedRegionId = (latitude.HasValue || !string.IsNullOrWhiteSpace(province))
            ? LocationRoutingService.ResolveRegionId(latitude, province)
            : fallbackRegionId;

        AdminRegionScopeHelper.SetScopeCookies(Response, scopedRegionId, latitude, province);
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet("trips")]
    public IActionResult Trips()
    {
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var fallbackRegionId = ParseRegionId(User.FindFirst("regionId")?.Value);
        var scopedRegionId = AdminRegionScopeHelper.GetScopedRegionId(Request, fallbackRegionId);
        ViewBag.ScopeLatitude = AdminRegionScopeHelper.GetScopeLatitudeText(Request);
        ViewBag.ScopeRegionId = scopedRegionId;

        return View();
    }

    [HttpGet("nearby-drivers")]
    public IActionResult NearbyDrivers()
    {
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var fallbackRegionId = ParseRegionId(User.FindFirst("regionId")?.Value);
        var scopedRegionId = AdminRegionScopeHelper.GetScopedRegionId(Request, fallbackRegionId);
        ViewBag.ScopeLatitude = AdminRegionScopeHelper.GetScopeLatitudeText(Request);
        ViewBag.ScopeRegionId = scopedRegionId;

        return View();
    }

    private async Task<(int UserId, string Name, string Email, int RegionId)?> FindAdminAsync(string email, string password)
    {
        var north = await FindAdminInConnectionAsync(_db.GetConnection(20), email, password);
        if (north is not null)
            return north;

        return await FindAdminInConnectionAsync(_db.GetConnection(10), email, password);
    }

    private static async Task<(int UserId, string Name, string Email, int RegionId)?> FindAdminInConnectionAsync(
        NpgsqlConnection conn,
        string email,
        string password)
    {
        await using (conn)
        {
            await conn.OpenAsync();
            const string sql = @"
SELECT UserID, Email, COALESCE(Name, 'Admin') AS Name, COALESCE(RegionID, 2) AS RegionID,
       CASE WHEN LOWER(IsActive::text) IN ('1','t','true') THEN TRUE ELSE FALSE END AS IsActive
FROM Users
WHERE Email = @email AND Password = @pwd AND Role = 'Admin'
LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@pwd", password);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            if (!reader.GetBoolean(reader.GetOrdinal("IsActive")))
                return null;

            return (
                reader.GetInt32(reader.GetOrdinal("UserID")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetInt32(reader.GetOrdinal("RegionID"))
            );
        }
    }

    private string GenerateAdminToken(int userId, string name, string email, int regionId)
    {
        var jwtKey = _config["Jwt:Key"] ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, name ?? "Admin"),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new("regionId", regionId.ToString()),
            new("role", "Admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "RideAPI",
            audience: _config["Jwt:Audience"] ?? "RideApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static int ParseRegionId(string? rawRegionId)
    {
        if (int.TryParse(rawRegionId, out var regionId) && (regionId == 1 || regionId == 2))
            return regionId;

        return 2;
    }
}
