using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RideAPI.Services;

namespace RideAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly PasswordHasherService _passwords;
        private readonly JwtTokenService _jwt;
        private readonly RefreshTokenService _refreshTokens;

        public AuthController(
            DatabaseService db,
            IConfiguration config,
            IWebHostEnvironment env,
            PasswordHasherService passwords,
            JwtTokenService jwt,
            RefreshTokenService refreshTokens)
        {
            _db = db;
            _config = config;
            _env = env;
            _passwords = passwords;
            _jwt = jwt;
            _refreshTokens = refreshTokens;
        }

        // POST: api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var email = request.Email?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Email và mật khẩu không được để trống." });

            try
            {
                const string sql = @"
                    SELECT u.UserID, u.Email, u.Role, u.CustomerID, u.DriverID,
                           COALESCE(c.FullName, d.Name, u.Name) AS DisplayName,
                           COALESCE(c.Phone, d.Phone, u.Phone) AS DisplayPhone,
                           u.Password,
                           u.IsActive,
                           COALESCE(u.RegionID, @regionId) AS RegionID
                    FROM Users u
                    LEFT JOIN Customers c ON c.CustomerID = u.CustomerID
                    LEFT JOIN Drivers d ON d.DriverID = u.DriverID
                    WHERE LOWER(u.Email) = LOWER(@email)
                      AND NOT COALESCE(u.IsDeleted, FALSE)
                    LIMIT 1";

                AuthUser? authenticated = null;
                string? authenticatedShard = null;
                var wrongPasswordOnAnyShard = false;
                var shardConnectionFailed = false;

                foreach (var shard in new[] { "NORTH", "SOUTH" })
                {
                    try
                    {
                        await using var conn = await _db.GetConnectionAsync(shard, isWrite: false);
                        await using var cmd = new NpgsqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@regionId", shard == "NORTH" ? 1 : 2);
                        await using var reader = await cmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                            continue;

                        var storedPassword = reader.GetString(reader.GetOrdinal("Password"));
                        if (!_passwords.VerifyPassword(password, storedPassword))
                        {
                            wrongPasswordOnAnyShard = true;
                            continue;
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("IsActive")) && !reader.GetBoolean(reader.GetOrdinal("IsActive")))
                            return Unauthorized(new { message = "Tài khoản đã bị khóa." });

                        authenticated = ReadAuthUserFromLoginReader(reader);
                        authenticatedShard = shard;
                        break;
                    }
                    catch (NpgsqlException)
                    {
                        shardConnectionFailed = true;
                    }
                    catch (InvalidOperationException)
                    {
                        shardConnectionFailed = true;
                    }
                }

                if (authenticated is null)
                {
                    if (wrongPasswordOnAnyShard)
                        return Unauthorized(new { message = "Mật khẩu không đúng." });

                    if (shardConnectionFailed)
                        return StatusCode(503, new { message = "Không kết nối được CSDL phân tán. Thử lại sau." });

                    return Unauthorized(new { message = "Không tìm thấy tài khoản với email này." });
                }

                var u = authenticated.Value;
                var token = _jwt.GenerateAccessToken(u.UserId, u.Name, u.Email, u.RegionId, u.Role, u.CustomerId, u.DriverId);

                // Đăng nhập đọc được từ replica (isWrite:false) nhưng refresh token cần ghi primary — khi master sập vẫn cấp access token.
                RefreshTokenResult? refresh = null;
                try
                {
                    refresh = await _refreshTokens.CreateAsync(authenticatedShard!, u.UserId, u.RegionId, u.Role, token.JwtId);
                }
                catch (NpgsqlException)
                {
                    // Bỏ qua: phiên chỉ dùng access token tới khi hết hạn.
                }
                catch (InvalidOperationException)
                {
                    // Master miền sập — không ghi được refresh token.
                }

                return Ok(new
                {
                    message = refresh is null
                        ? "Đăng nhập thành công (master miền đang sập — không lưu refresh token; hãy đăng nhập lại khi hết phiên)."
                        : "Đăng nhập thành công.",
                    token = token.Token,
                    accessToken = token.Token,
                    expiresAtUtc = token.ExpiresAtUtc,
                    refreshToken = refresh?.Token,
                    refreshTokenExpiresAtUtc = refresh?.ExpiresAtUtc,
                    sessionWithoutRefresh = refresh is null,
                    userId = u.UserId,
                    role = u.Role,
                    customerId = u.CustomerId,
                    driverId = u.DriverId,
                    name = u.Name,
                    phone = u.Phone,
                    email = u.Email,
                    regionId = u.RegionId,
                    region = authenticatedShard
                });
            }
            catch (NpgsqlException ex)
            {
                return StatusCode(503, new { message = "Khu vực đang bảo trì.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(503, new { message = "Khu vực đang bảo trì.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
        }

        // POST: api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var name = request.Name?.Trim() ?? string.Empty;
            var email = request.Email?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;
            var phone = request.Phone?.Trim() ?? string.Empty;

            var validationError = ValidateRegister(name, email, phone, password);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var region = LocationRoutingService.ResolveRegion(request.Latitude, request.Province);
            int regionId = region == "NORTH" ? 1 : 2;

            try
            {
                using var conn = await _db.GetConnectionAsync(region, isWrite: true);

                const string checkSql = @"
SELECT
    EXISTS (SELECT 1 FROM Users WHERE LOWER(Email) = LOWER(@email)) AS EmailExists,
    EXISTS (SELECT 1 FROM Users WHERE Phone = @phone AND BTRIM(COALESCE(Phone, '')) <> '') AS UserPhoneExists,
    EXISTS (SELECT 1 FROM Customers WHERE Phone = @phone AND BTRIM(COALESCE(Phone, '')) <> '') AS CustomerPhoneExists";
                await using var checkCmd = new NpgsqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@email", email);
                checkCmd.Parameters.AddWithValue("@phone", phone);
                await using (var checkReader = await checkCmd.ExecuteReaderAsync())
                {
                    if (await checkReader.ReadAsync())
                    {
                        if (checkReader.GetBoolean(checkReader.GetOrdinal("EmailExists")))
                            return Conflict(new { message = "Email đã được đăng ký." });
                        if (checkReader.GetBoolean(checkReader.GetOrdinal("UserPhoneExists"))
                            || checkReader.GetBoolean(checkReader.GetOrdinal("CustomerPhoneExists")))
                            return Conflict(new { message = "Số điện thoại đã được đăng ký." });
                    }
                }

                await using var tx = await conn.BeginTransactionAsync();

                const string insertCustomerSql = @"INSERT INTO Customers (FullName, Phone, Email) VALUES (@name, @phone, @email) RETURNING CustomerID";
                await using var customerCmd = new NpgsqlCommand(insertCustomerSql, conn, tx);
                customerCmd.Parameters.AddWithValue("@name", name);
                customerCmd.Parameters.AddWithValue("@phone", phone);
                customerCmd.Parameters.AddWithValue("@email", email);
                var newCustomerId = Convert.ToInt32(await customerCmd.ExecuteScalarAsync());

                const string insertUserSql = @"INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive)
                                      VALUES (@email, @pwd, 'Customer', @cid, @name, @phone, @region, TRUE)
                                      RETURNING UserID";
                await using var userCmd = new NpgsqlCommand(insertUserSql, conn, tx);
                userCmd.Parameters.AddWithValue("@email", email);
                userCmd.Parameters.AddWithValue("@pwd", password);
                userCmd.Parameters.AddWithValue("@cid", newCustomerId);
                userCmd.Parameters.AddWithValue("@name", name);
                userCmd.Parameters.AddWithValue("@phone", phone);
                userCmd.Parameters.AddWithValue("@region", regionId);
                var newUserId = Convert.ToInt32(await userCmd.ExecuteScalarAsync());

                await tx.CommitAsync();

                var token = _jwt.GenerateAccessToken(newUserId, name, email, regionId, "Customer", newCustomerId, null);
                var refreshToken = await _refreshTokens.CreateAsync(region, newUserId, regionId, "Customer", token.JwtId);

                return Ok(new
                {
                    message = "Đăng ký thành công.",
                    token = token.Token,
                    accessToken = token.Token,
                    expiresAtUtc = token.ExpiresAtUtc,
                    refreshToken = refreshToken.Token,
                    refreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
                    userId = newUserId,
                    role = "Customer",
                    customerId = newCustomerId,
                    driverId = (int?)null,
                    name,
                    email,
                    regionId,
                    region
                });
            }
            catch (NpgsqlException ex)
            {
                if (string.Equals(ex.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal))
                    return Conflict(new { message = "Email hoặc số điện thoại đã được đăng ký." });

                return StatusCode(503, new { message = "Đăng ký thất bại.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { message = "Thiếu refresh token." });

            var preferredRegion = LocationRoutingService.ResolveRegion(request.Latitude, request.Province);
            var found = await FindRefreshTokenAsync(preferredRegion, request.RefreshToken);
            if (found is null)
                return Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn." });

            var (region, stored) = found.Value;
            var user = await ReadUserByIdAsync(region, stored.UserId);
            if (user is null || !user.Value.IsActive)
                return Unauthorized(new { message = "Tài khoản không hợp lệ hoặc đã bị khóa." });

            var token = _jwt.GenerateAccessToken(
                user.Value.UserId,
                user.Value.Name,
                user.Value.Email,
                user.Value.RegionId,
                user.Value.Role,
                user.Value.CustomerId,
                user.Value.DriverId);
            var refreshToken = await _refreshTokens.CreateAsync(region, user.Value.UserId, user.Value.RegionId, user.Value.Role, token.JwtId);
            await _refreshTokens.RevokeAsync(region, request.RefreshToken, refreshToken.Token);

            return Ok(new
            {
                message = "Làm mới phiên thành công.",
                token = token.Token,
                accessToken = token.Token,
                expiresAtUtc = token.ExpiresAtUtc,
                refreshToken = refreshToken.Token,
                refreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
                userId = user.Value.UserId,
                role = user.Value.Role,
                customerId = user.Value.CustomerId,
                driverId = user.Value.DriverId,
                name = user.Value.Name,
                phone = user.Value.Phone,
                email = user.Value.Email,
                regionId = user.Value.RegionId,
                region
            });
        }

        [Authorize]
        [HttpGet("session")]
        public IActionResult GetSession()
        {
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var name = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            var email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

            _ = int.TryParse(User.FindFirst("regionId")?.Value, out var regionId);
            int? customerId = int.TryParse(User.FindFirst("customerId")?.Value, out var cid) ? cid : null;
            int? driverId = int.TryParse(User.FindFirst("driverId")?.Value, out var did) ? did : null;

            DateTime? expiresAtUtc = null;
            var expRaw = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value ?? User.FindFirst("exp")?.Value;
            if (long.TryParse(expRaw, out var expUnix))
                expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

            return Ok(new
            {
                isAuthenticated = true,
                userId,
                role,
                customerId,
                driverId,
                name,
                email,
                regionId,
                expiresAtUtc
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
        {
            var regionId = int.TryParse(User.FindFirst("regionId")?.Value, out var rid) ? rid : 2;
            var region = regionId == 1 ? "NORTH" : "SOUTH";
            var userId = int.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                ? uid
                : (int?)null;
            var jwtId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var expRaw = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value ?? User.FindFirst("exp")?.Value;
            var expiresAtUtc = long.TryParse(expRaw, out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
                : DateTime.UtcNow.AddMinutes(5);

            var serverRevokeOk = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
                    await _refreshTokens.RevokeAsync(region, request.RefreshToken);
                await _refreshTokens.RevokeJwtAsync(region, jwtId, userId, expiresAtUtc);
            }
            catch (NpgsqlException)
            {
                serverRevokeOk = false;
            }
            catch (InvalidOperationException)
            {
                serverRevokeOk = false;
            }

            return Ok(new
            {
                message = serverRevokeOk
                    ? "Đăng xuất thành công."
                    : "Đăng xuất thành công (CSDL master đang sập — token đã xóa phía client; đăng nhập lại khi hệ thống ổn định).",
                serverRevokeOk
            });
        }

        private async Task<(string Region, StoredRefreshToken Token)?> FindRefreshTokenAsync(string preferredRegion, string refreshToken)
        {
            foreach (var region in new[] { preferredRegion, preferredRegion == "NORTH" ? "SOUTH" : "NORTH" }.Distinct())
            {
                var stored = await _refreshTokens.FindValidAsync(region, refreshToken);
                if (stored is not null)
                    return (region, stored);
            }

            return null;
        }

        private async Task<AuthUser?> ReadUserByIdAsync(string region, int userId)
        {
            await using var conn = await _db.GetConnectionAsync(region, isWrite: false);
            const string sql = @"
SELECT u.UserID, u.Email, u.Role, u.CustomerID, u.DriverID,
       COALESCE(c.FullName, d.Name, u.Name, '') AS DisplayName,
       COALESCE(c.Phone, d.Phone, u.Phone, '') AS DisplayPhone,
       COALESCE(u.RegionID, @regionId) AS RegionID,
       CASE WHEN LOWER(u.IsActive::text) IN ('1','t','true') THEN TRUE ELSE FALSE END AS IsActive
FROM Users u
LEFT JOIN Customers c ON c.CustomerID = u.CustomerID
LEFT JOIN Drivers d ON d.DriverID = u.DriverID
WHERE u.UserID = @userId
  AND NOT COALESCE(u.IsDeleted, FALSE)
LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@regionId", region == "NORTH" ? 1 : 2);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new AuthUser(
                reader.GetInt32(reader.GetOrdinal("UserID")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("Role")),
                reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32(reader.GetOrdinal("CustomerID")),
                reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32(reader.GetOrdinal("DriverID")),
                reader.GetString(reader.GetOrdinal("DisplayName")),
                reader.GetString(reader.GetOrdinal("DisplayPhone")),
                reader.GetInt32(reader.GetOrdinal("RegionID")),
                reader.GetBoolean(reader.GetOrdinal("IsActive")));
        }

        private static AuthUser ReadAuthUserFromLoginReader(NpgsqlDataReader reader)
        {
            return new AuthUser(
                reader.GetInt32(reader.GetOrdinal("UserID")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("Role")),
                reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32(reader.GetOrdinal("CustomerID")),
                reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32(reader.GetOrdinal("DriverID")),
                reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DisplayName")),
                reader.IsDBNull(reader.GetOrdinal("DisplayPhone")) ? string.Empty : reader.GetString(reader.GetOrdinal("DisplayPhone")),
                reader.GetInt32(reader.GetOrdinal("RegionID")),
                !reader.IsDBNull(reader.GetOrdinal("IsActive")) && reader.GetBoolean(reader.GetOrdinal("IsActive")));
        }

        private static string? ValidateRegister(string name, string email, string phone, string password)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Họ tên không được để trống.";
            if (!IsValidEmail(email))
                return "Email không hợp lệ.";
            if (!IsValidPhone(phone))
                return "Số điện thoại không hợp lệ.";
            if (password.Length < 6)
                return "Mật khẩu phải có ít nhất 6 ký tự.";

            return null;
        }

        private static bool IsValidEmail(string email)
            => new EmailAddressAttribute().IsValid(email);

        private static bool IsValidPhone(string phone)
            => Regex.IsMatch(phone, @"^\+?[0-9]{9,15}$");
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public string? Province { get; set; }
    }

    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public string? Province { get; set; }
    }

    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public string? Province { get; set; }
    }

    public class LogoutRequest
    {
        public string? RefreshToken { get; set; }
    }

    internal readonly record struct AuthUser(
        int UserId,
        string Email,
        string Role,
        int? CustomerId,
        int? DriverId,
        string Name,
        string Phone,
        int RegionId,
        bool IsActive);
}