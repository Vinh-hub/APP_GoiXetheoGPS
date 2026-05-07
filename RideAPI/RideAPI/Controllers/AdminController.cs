using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RideAPI.Models.ViewModels;
using RideAPI.Services;

namespace RideAPI.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly DatabaseService _db;
    private readonly PasswordHasherService _passwords;
    private readonly JwtTokenService _jwt;
    private readonly IWebHostEnvironment _env;

    public AdminController(DatabaseService db, PasswordHasherService passwords, JwtTokenService jwt, IWebHostEnvironment env)
    {
        _db = db;
        _passwords = passwords;
        _jwt = jwt;
        _env = env;
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

        var token = _jwt.GenerateAccessToken(admin.Value.UserId, admin.Value.Name, admin.Value.Email, admin.Value.RegionId, "Admin", null, null);
        Response.Cookies.Append("admin_jwt", token.Token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = !_env.IsDevelopment() || Request.IsHttps,
            Expires = token.ExpiresAtUtc
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

    [AllowAnonymous]
    [HttpGet("unauthorized")]
    public IActionResult UnauthorizedPage()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        ViewBag.StatusCode = 403;
        ViewBag.Title = "Không có quyền truy cập";
        ViewBag.Message = "Tài khoản hiện tại không có quyền truy cập chức năng này.";
        return View("Status");
    }

    [AllowAnonymous]
    [HttpGet("status/{code:int}")]
    public IActionResult StatusPage(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;
        ViewBag.Title = code switch
        {
            401 => "Cần đăng nhập",
            403 => "Không có quyền truy cập",
            404 => "Không tìm thấy trang",
            _ => "Đã xảy ra lỗi"
        };
        ViewBag.Message = code switch
        {
            401 => "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
            403 => "Bạn không có quyền truy cập tài nguyên này.",
            404 => "Trang bạn yêu cầu không tồn tại.",
            _ => "Hệ thống gặp lỗi khi xử lý yêu cầu."
        };
        return View("Status");
    }

    private async Task<(int UserId, string Name, string Email, int RegionId)?> FindAdminAsync(string email, string password)
    {
        var north = await FindAdminInConnectionAsync(_db.GetConnection(20), email, password, _passwords);
        if (north is not null)
            return north;

        return await FindAdminInConnectionAsync(_db.GetConnection(10), email, password, _passwords);
    }

    private static async Task<(int UserId, string Name, string Email, int RegionId)?> FindAdminInConnectionAsync(
        NpgsqlConnection conn,
        string email,
        string password,
        PasswordHasherService passwords)
    {
        await using (conn)
        {
            await conn.OpenAsync();
            const string sql = @"
SELECT UserID, Email, Password, COALESCE(Name, 'Admin') AS Name, COALESCE(RegionID, 2) AS RegionID,
       CASE WHEN LOWER(IsActive::text) IN ('1','t','true') THEN TRUE ELSE FALSE END AS IsActive
FROM Users
WHERE LOWER(Email) = LOWER(@email) AND Role = 'Admin'
LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@email", email);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            if (!passwords.VerifyPassword(password, reader.GetString(reader.GetOrdinal("Password"))))
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

    private static int ParseRegionId(string? rawRegionId)
    {
        if (int.TryParse(rawRegionId, out var regionId) && (regionId == 1 || regionId == 2))
            return regionId;

        return 2;
    }
}
