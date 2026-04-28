using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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

        public AuthController(DatabaseService db, IConfiguration config, IWebHostEnvironment env)
        {
            _db = db;
            _config = config;
            _env = env;
        }

        // POST: api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var email = request.Email?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Email và mật khẩu không được để trống." });

            var region = LocationRoutingService.ResolveRegion(request.Latitude, request.Province);
            try
            {
                using var conn = await _db.GetConnectionAsync(region, isWrite: false);

                const string sql = @"
                    SELECT u.UserID, u.Email, u.Role, u.CustomerID, u.DriverID,
                           COALESCE(c.FullName, d.Name, u.Name) AS DisplayName,
                           COALESCE(c.Phone, d.Phone, u.Phone) AS DisplayPhone,
                           u.IsActive
                    FROM Users u
                    LEFT JOIN Customers c ON c.CustomerID = u.CustomerID
                    LEFT JOIN Drivers d ON d.DriverID = u.DriverID
                    WHERE u.Email = @email AND u.Password = @pwd
                    LIMIT 1";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@pwd", password);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return Unauthorized(new { message = "Sai email hoặc mật khẩu." });

                if (!reader.IsDBNull(reader.GetOrdinal("IsActive")) && !reader.GetBoolean(reader.GetOrdinal("IsActive")))
                    return Unauthorized(new { message = "Tài khoản đã bị khóa." });

                var userId = reader.GetInt32(reader.GetOrdinal("UserID"));
                var accountEmail = reader.GetString(reader.GetOrdinal("Email"));
                var role = reader.GetString(reader.GetOrdinal("Role"));
                var displayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DisplayName"));
                var displayPhone = reader.IsDBNull(reader.GetOrdinal("DisplayPhone")) ? string.Empty : reader.GetString(reader.GetOrdinal("DisplayPhone"));
                int? customerId = reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32(reader.GetOrdinal("CustomerID"));
                int? driverId = reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32(reader.GetOrdinal("DriverID"));
                int regionId = region == "NORTH" ? 1 : 2;

                var token = GenerateJwtToken(userId, displayName, accountEmail, regionId, role, customerId, driverId);

                return Ok(new
                {
                    message = "Đăng nhập thành công.",
                    token,
                    userId,
                    role,
                    customerId,
                    driverId,
                    name = displayName,
                    phone = displayPhone,
                    email = accountEmail,
                    regionId,
                    region
                });
            }
            catch (NpgsqlException ex)
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

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });

            var region = LocationRoutingService.ResolveRegion(request.Latitude, request.Province);
            int regionId = region == "NORTH" ? 1 : 2;

            try
            {
                using var conn = await _db.GetConnectionAsync(region, isWrite: true);

                const string checkSql = "SELECT COUNT(*) FROM Users WHERE Email = @email";
                await using var checkCmd = new NpgsqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@email", email);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count > 0)
                    return Conflict(new { message = "Email đã được đăng ký." });

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

                var token = GenerateJwtToken(newUserId, name, email, regionId, "Customer", newCustomerId, null);

                return Ok(new
                {
                    message = "Đăng ký thành công.",
                    token,
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
                    return Conflict(new { message = "Email đã được đăng ký." });

                return StatusCode(503, new { message = "Đăng ký thất bại.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
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
        public IActionResult Logout()
        {
            return Ok(new { message = "Đăng xuất thành công." });
        }

        private string GenerateJwtToken(int userId, string name, string email, int regionId, string role, int? customerId, int? driverId)
        {
            var jwtKey = _config["Jwt:Key"] ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Name, name ?? string.Empty),
                new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
                new("regionId", regionId.ToString()),
                new("role", role ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (customerId.HasValue) claims.Add(new Claim("customerId", customerId.Value.ToString()));
            if (driverId.HasValue) claims.Add(new Claim("driverId", driverId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "RideAPI",
                audience: _config["Jwt:Audience"] ?? "RideApp",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
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
}