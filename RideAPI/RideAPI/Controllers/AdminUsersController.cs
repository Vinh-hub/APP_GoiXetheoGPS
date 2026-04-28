using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RideAPI.Models.ViewModels;
using RideAPI.Services;

namespace RideAPI.Controllers;

[Authorize]
[Route("admin/users")]
public class AdminUsersController : Controller
{
    private readonly DatabaseService _db;

    public AdminUsersController(DatabaseService db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? role, string? status, double? latitude, string? province)
    {
        if (!TryGetAdminContext(out var adminRegionId, out _))
            return Forbid();

        var regionId = (latitude.HasValue || !string.IsNullOrWhiteSpace(province))
            ? ResolveTargetRegionId(adminRegionId, latitude, province)
            : AdminRegionScopeHelper.GetScopedRegionId(Request, adminRegionId);
        var users = new List<AdminUserListItemViewModel>();
        await using var conn = _db.GetConnection(regionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        var sql = @"
SELECT UserID, Email, Role, CustomerID, DriverID, COALESCE(Name, '') AS Name, COALESCE(Phone, '') AS Phone,
       CASE WHEN LOWER(IsActive::text) IN ('1','t','true') THEN TRUE ELSE FALSE END AS IsActive,
       RegionID
FROM Users
WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(keyword))
            sql += " AND (LOWER(Email) LIKE @keyword OR LOWER(COALESCE(Name, '')) LIKE @keyword OR LOWER(COALESCE(Phone, '')) LIKE @keyword)";

        if (!string.IsNullOrWhiteSpace(role))
            sql += " AND Role = @role";

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            sql += " AND LOWER(IsActive::text) IN ('1','t','true')";
        else if (string.Equals(status, "locked", StringComparison.OrdinalIgnoreCase))
            sql += " AND LOWER(IsActive::text) NOT IN ('1','t','true')";

        sql += " ORDER BY UserID DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(keyword))
            cmd.Parameters.AddWithValue("@keyword", $"%{keyword.Trim().ToLowerInvariant()}%");

        if (!string.IsNullOrWhiteSpace(role))
            cmd.Parameters.AddWithValue("@role", role.Trim());

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            users.Add(new AdminUserListItemViewModel
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserID")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                CustomerId = reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32(reader.GetOrdinal("CustomerID")),
                DriverId = reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32(reader.GetOrdinal("DriverID")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Phone = reader.GetString(reader.GetOrdinal("Phone")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                RegionId = reader.IsDBNull(reader.GetOrdinal("RegionID")) ? null : reader.GetInt32(reader.GetOrdinal("RegionID"))
            });
        }

        ViewBag.Keyword = keyword ?? string.Empty;
        ViewBag.Role = role ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.Latitude = latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? AdminRegionScopeHelper.GetScopeLatitudeText(Request);
        ViewBag.Province = province ?? AdminRegionScopeHelper.GetScopeProvince(Request);
        ViewBag.RegionScope = AdminRegionScopeHelper.GetScopeLabel(regionId);

        return View(users);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        if (!TryGetAdminContext(out var adminRegionId, out _))
            return Forbid();

        var regionId = AdminRegionScopeHelper.GetScopedRegionId(Request, adminRegionId);
        return View(new AdminUserUpsertViewModel { RegionId = regionId, IsActive = true, Role = "Customer" });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserUpsertViewModel model)
    {
        if (!TryGetAdminContext(out var adminRegionId, out _))
            return Forbid();

        model.RegionId = ResolveTargetRegionId(
            AdminRegionScopeHelper.GetScopedRegionId(Request, adminRegionId),
            model.Latitude,
            model.Province);
        ValidateRoleBindings(model, isCreate: true);

        if (!ModelState.IsValid)
            return View(model);

        await using var conn = _db.GetConnection(model.RegionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO Users (Email, Password, Role, CustomerID, DriverID, Name, Phone, RegionID, IsActive)
VALUES (@email, @password, @role, @customerId, @driverId, @name, @phone, @regionId, @isActive)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", model.Email.Trim());
        cmd.Parameters.AddWithValue("@password", model.Password);
        cmd.Parameters.AddWithValue("@role", model.Role.Trim());
        cmd.Parameters.AddWithValue("@customerId", (object?)model.CustomerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@driverId", (object?)model.DriverId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@name", model.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@phone", model.Phone ?? string.Empty);
        cmd.Parameters.AddWithValue("@regionId", model.RegionId);
        cmd.Parameters.AddWithValue("@isActive", model.IsActive);

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            ModelState.AddModelError(string.Empty, $"Không thể thêm user: {ex.MessageText}");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        if (!TryGetAdminContext(out var adminRegionId, out _))
            return Forbid();

        var regionId = AdminRegionScopeHelper.GetScopedRegionId(Request, adminRegionId);
        await using var conn = _db.GetConnection(regionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        const string sql = @"
SELECT UserID, Email, Role, CustomerID, DriverID, COALESCE(Name,'') AS Name, COALESCE(Phone,'') AS Phone,
       CASE WHEN LOWER(IsActive::text) IN ('1','t','true') THEN TRUE ELSE FALSE END AS IsActive,
       COALESCE(RegionID, @regionId) AS RegionID
FROM Users
WHERE UserID = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@regionId", regionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return NotFound();

        var model = new AdminUserUpsertViewModel
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserID")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            Role = reader.GetString(reader.GetOrdinal("Role")),
            CustomerId = reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32(reader.GetOrdinal("CustomerID")),
            DriverId = reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32(reader.GetOrdinal("DriverID")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Phone = reader.GetString(reader.GetOrdinal("Phone")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            RegionId = reader.GetInt32(reader.GetOrdinal("RegionID")),
            Latitude = reader.GetInt32(reader.GetOrdinal("RegionID")) == 1 ? 21.0285 : 10.7769,
            Longitude = reader.GetInt32(reader.GetOrdinal("RegionID")) == 1 ? 105.8542 : 106.7009
        };

        return View(model);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminUserUpsertViewModel model)
    {
        if (!TryGetAdminContext(out var adminRegionId, out _))
            return Forbid();

        if (id != model.UserId)
            return BadRequest();

        model.RegionId = ResolveTargetRegionId(
            AdminRegionScopeHelper.GetScopedRegionId(Request, adminRegionId),
            model.Latitude,
            model.Province);
        ValidateRoleBindings(model, isCreate: false);

        if (!ModelState.IsValid)
            return View(model);

        await using var conn = _db.GetConnection(model.RegionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        const string sql = @"
UPDATE Users
SET Email = @email,
    Password = CASE WHEN @password = '' THEN Password ELSE @password END,
    Role = @role,
    CustomerID = @customerId,
    DriverID = @driverId,
    Name = @name,
    Phone = @phone,
    RegionID = @regionId,
    IsActive = @isActive
WHERE UserID = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@email", model.Email.Trim());
        cmd.Parameters.AddWithValue("@password", model.Password ?? string.Empty);
        cmd.Parameters.AddWithValue("@role", model.Role.Trim());
        cmd.Parameters.AddWithValue("@customerId", (object?)model.CustomerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@driverId", (object?)model.DriverId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@name", model.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@phone", model.Phone ?? string.Empty);
        cmd.Parameters.AddWithValue("@regionId", model.RegionId);
        cmd.Parameters.AddWithValue("@isActive", model.IsActive);

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            ModelState.AddModelError(string.Empty, $"Không thể cập nhật user: {ex.MessageText}");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetAdminContext(out var regionId, out var currentUserId))
            return Forbid();

        if (id == currentUserId)
            return BadRequest("Không thể xóa tài khoản admin đang đăng nhập.");

        await using var conn = _db.GetConnection(regionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        const string sql = "DELETE FROM Users WHERE UserID = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("toggle-lock/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        if (!TryGetAdminContext(out var regionId, out var currentUserId))
            return Forbid();

        if (id == currentUserId)
            return BadRequest("Không thể tự khóa tài khoản đang đăng nhập.");

        await using var conn = _db.GetConnection(regionId == 1 ? 20 : 10);
        await conn.OpenAsync();

        const string sql = @"
UPDATE Users
SET IsActive = CASE
    WHEN LOWER(IsActive::text) IN ('1','t','true') THEN FALSE
    ELSE TRUE
END
WHERE UserID = @id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetAdminContext(out int regionId, out int currentUserId)
    {
        regionId = 2;
        currentUserId = 0;

        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return false;

        _ = int.TryParse(User.FindFirst("regionId")?.Value, out regionId);
        if (regionId != 1 && regionId != 2)
            regionId = 2;

        _ = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out currentUserId);
        return true;
    }

    private void ValidateRoleBindings(AdminUserUpsertViewModel model, bool isCreate)
    {
        var role = (model.Role ?? string.Empty).Trim();
        if (role is not ("Admin" or "Customer" or "Driver"))
            ModelState.AddModelError(nameof(model.Role), "Role chỉ chấp nhận: Admin, Customer, Driver.");

        if (isCreate && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password là bắt buộc khi tạo user.");

        if (role == "Admin")
        {
            model.CustomerId = null;
            model.DriverId = null;
            return;
        }

        if (role == "Customer")
        {
            model.DriverId = null;
            if (!model.CustomerId.HasValue)
                ModelState.AddModelError(nameof(model.CustomerId), "CustomerId là bắt buộc cho role Customer.");
            return;
        }

        model.CustomerId = null;
        if (!model.DriverId.HasValue)
            ModelState.AddModelError(nameof(model.DriverId), "DriverId là bắt buộc cho role Driver.");
    }

    private static int ResolveTargetRegionId(int fallbackRegionId, double? latitude, string? province)
    {
        if (latitude.HasValue || !string.IsNullOrWhiteSpace(province))
            return LocationRoutingService.ResolveRegionId(latitude, province);

        return fallbackRegionId is 1 or 2 ? fallbackRegionId : 2;
    }
}
