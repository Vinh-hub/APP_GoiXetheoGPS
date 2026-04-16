using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using RideAPI.Services;

namespace RideAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
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
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromBody] LoginRequest request)
        {
            var email = request.Email?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Email và mật khẩu không được để trống." });

            double lat = GetLatitude(userLatitude);
            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                var sql = @"SELECT u.UserID, u.Email, u.Role, u.CustomerID, u.DriverID, u.IsActive,
                                   COALESCE(c.FullName, d.Name, u.Name) AS DisplayName,
                                   COALESCE(c.Phone, d.Phone, u.Phone) AS DisplayPhone
                            FROM Users u
                            LEFT JOIN Customers c ON c.CustomerID = u.CustomerID
                            LEFT JOIN Drivers d ON d.DriverID = u.DriverID
                            WHERE u.Email = @email AND u.Password = @pwd
                            LIMIT 1";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@pwd", password);
                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return Unauthorized(new { message = "Sai email hoặc mật khẩu." });

                bool isActive = reader.GetBoolean("IsActive");
                if (!isActive)
                    return Unauthorized(new { message = "Tài khoản đã bị khóa." });

                var userId = reader.GetInt32("UserID");
                var accountEmail = reader.GetString("Email");
                var role = reader.GetString("Role");
                var displayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "" : reader.GetString("DisplayName");
                var displayPhone = reader.IsDBNull(reader.GetOrdinal("DisplayPhone")) ? "" : reader.GetString("DisplayPhone");
                int? customerId = reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32("CustomerID");
                int? driverId = reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32("DriverID");
                int regionId = lat > 16 ? 1 : 2;

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
                    regionId
                });
            }
            catch (MySqlException ex)
            {
                return StatusCode(503, new { message = "Khu vực đang bảo trì.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromBody] RegisterRequest request)
        {
            var name = request.Name?.Trim() ?? string.Empty;
            var email = request.Email?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;
            var phone = request.Phone?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });

            double lat = GetLatitude(userLatitude);
            int regionId = lat > 16 ? 1 : 2;

            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                // Kiểm tra email tồn tại
                var checkSql = "SELECT COUNT(*) FROM Users WHERE Email = @email";
                using var checkCmd = new MySqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@email", email);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count > 0)
                    return Conflict(new { message = "Email đã được đăng ký." });

                using var tx = await conn.BeginTransactionAsync();

                // Thêm Customer
                var insertCustomerSql = @"INSERT INTO Customers (FullName, Phone, Email) VALUES (@name, @phone, @email)";
                using var customerCmd = new MySqlCommand(insertCustomerSql, conn, tx);
                customerCmd.Parameters.AddWithValue("@name", name);
                customerCmd.Parameters.AddWithValue("@phone", phone);
                customerCmd.Parameters.AddWithValue("@email", email);
                await customerCmd.ExecuteNonQueryAsync();
                var newCustomerId = (int)customerCmd.LastInsertedId;

                // Thêm User
                var insertUserSql = @"INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive)
                                      VALUES (@email, @pwd, 'Customer', @cid, @name, @phone, @region, 1)";
                using var userCmd = new MySqlCommand(insertUserSql, conn, tx);
                userCmd.Parameters.AddWithValue("@email", email);
                userCmd.Parameters.AddWithValue("@pwd", password);
                userCmd.Parameters.AddWithValue("@cid", newCustomerId);
                userCmd.Parameters.AddWithValue("@name", name);
                userCmd.Parameters.AddWithValue("@phone", phone);
                userCmd.Parameters.AddWithValue("@region", regionId);
                await userCmd.ExecuteNonQueryAsync();
                var newUserId = (int)userCmd.LastInsertedId;

                await tx.CommitAsync();

                // Tạo token cho user mới
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
                    regionId
                });
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062)
                    return Conflict(new { message = "Email đã được đăng ký." });
                return StatusCode(503, new { message = "Đăng ký thất bại.", detail = _env.IsDevelopment() ? ex.Message : null });
            }
        }

        private double GetLatitude(double? userLatitude)
        {
            if (userLatitude.HasValue && userLatitude.Value >= -90 && userLatitude.Value <= 90)
                return userLatitude.Value;
            if (Request.Headers.TryGetValue("X-User-Latitude", out var val) && double.TryParse(val, out var lat) && lat >= -90 && lat <= 90)
                return lat;
            return 10.8; // mặc định miền Nam
        }

        private string GenerateJwtToken(int userId, string name, string email, int regionId, string role, int? customerId, int? driverId)
        {
            var jwtKey = _config["Jwt:Key"] ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Name, name ?? ""),
                new(JwtRegisteredClaimNames.Email, email ?? ""),
                new("regionId", regionId.ToString()),
                new("role", role ?? ""),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            if (customerId.HasValue) claims.Add(new Claim("customerId", customerId.ToString()));
            if (driverId.HasValue) claims.Add(new Claim("driverId", driverId.ToString()));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "RideAPI",
                audience: _config["Jwt:Audience"] ?? "RideApp",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}