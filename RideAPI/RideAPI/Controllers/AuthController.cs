using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using RideAPI.Models;
using RideAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RideAPI.Controllers
{
    [Route("api/auth")]
    [ApiController] // API Controller: tự bind/validate request body
    [AllowAnonymous] // Đăng nhập / đăng ký công khai; toàn API mặc định yêu cầu JWT (Program.cs).
    public class AuthController : ControllerBase
    {
        // Kết nối DB theo khu vực (NorthDB/SouthDB) dựa vào vĩ độ trong header.
        private readonly DatabaseService _db;

        // Đọc cấu hình JWT, connection strings...
        private readonly IConfiguration _config;

        // Chỉ dùng để bật/tắt trả về "detail" khi lỗi (dev vs prod).
        private readonly IWebHostEnvironment _env;

        // .NET DI tự inject các dependency ở đây.
        public AuthController(DatabaseService db, IConfiguration config, IWebHostEnvironment env)
        {
            _db = db;
            _config = config;
            _env = env;
        }

        /// <summary>
        /// Đăng nhập bằng Email + Password.
        /// Users là bảng "tài khoản", liên kết 1-1 tới Customers hoặc Drivers.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromBody] LoginRequest request)
        {
            // Bước 1: Kiểm tra người dùng có nhập đủ thông tin chưa
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email và mật khẩu không được để trống." });

            // Bước 2: Đọc vĩ độ từ header để xác định kết nối DB nào
            //   > 16  → NorthDB (Hà Nội và các tỉnh miền Bắc)
            //   <= 16 → SouthDB (TP.HCM và các tỉnh miền Nam)
            double lat = GetLatitude(userLatitude);

            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                // Query lấy:
                // - thông tin account (UserID/Role/CustomerID/DriverID/IsActive...)
                // - thông tin hiển thị từ profile (Customers.FullName/Phone hoặc Drivers.Name/Phone)
                var sql = @"SELECT 
                                u.UserID,
                                u.Email,
                                u.Role,
                                u.CustomerID,
                                u.DriverID,
                                u.RegionID,
                                u.IsActive,
                                COALESCE(c.FullName, d.Name, u.Name) AS DisplayName,
                                COALESCE(c.Phone, d.Phone, u.Phone)  AS DisplayPhone
                            FROM Users u
                            LEFT JOIN Customers c ON c.CustomerID = u.CustomerID
                            LEFT JOIN Drivers   d ON d.DriverID   = u.DriverID
                            WHERE u.Email = @email AND u.Password = @pwd
                            LIMIT 1";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@email", request.Email.Trim());
                cmd.Parameters.AddWithValue("@pwd", request.Password);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return Unauthorized(new { message = "Sai email hoặc mật khẩu." });

                // Bước 6: Kiểm tra tài khoản có bị khóa không
                //   IsActive = 1 → bình thường
                //   IsActive = 0 → đã bị khóa
                bool isActive = reader.GetBoolean("IsActive");
                if (!isActive)
                    return Unauthorized(new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });

                var userId = reader.GetInt32("UserID");
                var email = reader.GetString("Email");
                var role = reader.GetString("Role"); // Customer | Driver
                var regionId = reader.GetInt32("RegionID");
                var displayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "" : reader.GetString("DisplayName");
                var displayPhone = reader.IsDBNull(reader.GetOrdinal("DisplayPhone")) ? "" : reader.GetString("DisplayPhone");
                int? customerId = reader.IsDBNull(reader.GetOrdinal("CustomerID")) ? null : reader.GetInt32("CustomerID");
                int? driverId = reader.IsDBNull(reader.GetOrdinal("DriverID")) ? null : reader.GetInt32("DriverID");

                // Tạo JWT token (để app gửi kèm Authorization: Bearer <token> ở các request sau)
                var token = GenerateJwt(
                    userId: userId.ToString(),
                    name: displayName,
                    email: email,
                    regionId: regionId,
                    role: role,
                    customerId: customerId,
                    driverId: driverId);

                // Bước 9: Trả về token và thông tin user cho app
                return Ok(new
                {
                    message = "Đăng nhập thành công.",
                    token,                      // JWT token, app lưu lại dùng cho các request sau
                    userId,
                    role,
                    customerId,
                    driverId,
                    name = displayName,
                    phone = displayPhone,
                    email,
                    regionId
                });
            }
            catch (MySqlException ex)
            {
                // MySQL lỗi kết nối / sai schema / DB read-only... trả về 503 để app biết "dịch vụ khu vực" không sẵn sàng.
                // Ở môi trường dev sẽ trả thêm detail để debug nhanh.
                return StatusCode(503, new
                {
                    message = "Khu vực này đang bảo trì. Vui lòng thử lại sau.",
                    detail = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }

        /// <summary>
        /// Đăng ký tài khoản mới cho khách hàng.
        /// Luồng tạo dữ liệu: Customers (profile) -> Users (account).
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromBody] RegisterRequest request)
        {
            // Bước 1: Kiểm tra người dùng có nhập đủ thông tin bắt buộc chưa
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });

            // Bước 2: Đọc vĩ độ từ header → xác định user thuộc vùng nào
            //   > 16  → RegionID = 1 (Miền Bắc) → lưu vào NorthDB
            //   <= 16 → RegionID = 2 (Miền Nam)  → lưu vào SouthDB
            double lat = GetLatitude(userLatitude);
            int regionId = lat > 16 ? 1 : 2;

            try
            {
                // Bước 3: Mở kết nối tới DB tương ứng
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                // Bước 4: Kiểm tra email này đã có trong DB chưa
                var checkSql = "SELECT COUNT(*) FROM Users WHERE Email = @email";
                using var checkCmd = new MySqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                // Nếu email đã tồn tại → không cho đăng ký trùng
                if (count > 0)
                    return Conflict(new { message = "Email này đã được đăng ký." });

                // Dùng transaction để tránh trường hợp:
                // - tạo Customer xong nhưng tạo User lỗi => dữ liệu bị "mồ côi".
                // Luồng tạo: Customers (profile) -> Users (account, Role='Customer', CustomerID).
                using var tx = await conn.BeginTransactionAsync();

                var insertCustomerSql = @"INSERT INTO Customers (FullName, Phone, Email)
                                          VALUES (@name, @phone, @email)";
                using var customerCmd = new MySqlCommand(insertCustomerSql, conn, tx);
                customerCmd.Parameters.AddWithValue("@name", request.Name.Trim());
                customerCmd.Parameters.AddWithValue("@phone", request.Phone?.Trim() ?? "");
                customerCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                await customerCmd.ExecuteNonQueryAsync();
                var newCustomerId = (int)customerCmd.LastInsertedId;

                var insertUserSql = @"INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive)
                                      VALUES (@email, @pwd, 'Customer', @cid, @name, @phone, @region, 1)";
                using var userCmd = new MySqlCommand(insertUserSql, conn, tx);
                userCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                userCmd.Parameters.AddWithValue("@pwd", request.Password);
                userCmd.Parameters.AddWithValue("@cid", newCustomerId);
                userCmd.Parameters.AddWithValue("@name", request.Name.Trim());
                userCmd.Parameters.AddWithValue("@phone", request.Phone?.Trim() ?? "");
                userCmd.Parameters.AddWithValue("@region", regionId);
                await userCmd.ExecuteNonQueryAsync();
                var newUserId = (int)userCmd.LastInsertedId;

                await tx.CommitAsync();

                // Trả token luôn để app đăng nhập ngay sau đăng ký.
                var token = GenerateJwt(
                    userId: newUserId.ToString(),
                    name: request.Name.Trim(),
                    email: request.Email.Trim(),
                    regionId: regionId,
                    role: "Customer",
                    customerId: newCustomerId,
                    driverId: null);

                // Bước 8: Trả về token và thông tin user cho app
                return Ok(new
                {
                    message = "Đăng ký thành công.",
                    token,
                    userId = newUserId,
                    role = "Customer",
                    customerId = newCustomerId,
                    driverId = (int?)null,
                    name = request.Name,
                    email = request.Email,
                    regionId
                });
            }
            catch (MySqlException ex)
            {
                return StatusCode(503, new
                {
                    message = "Khu vực này đang bảo trì. Vui lòng thử lại sau.",
                    detail = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }

        // HELPER 1: LẤY VĨ ĐỘ TỪ HEADER
        // Đọc header "X-User-Latitude" từ request gửi lên
        // Nếu không có header → dùng mặc định 10.8 (TP.HCM, miền Nam)
        private double GetLatitude(double? userLatitude)
        {
            if (userLatitude is double latFromParam)
                return latFromParam;

            if (Request.Headers.TryGetValue("X-User-Latitude", out var val) &&
                double.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var lat))
                return lat;

            return 10.8;
        }

        // HELPER 2: TẠO JWT TOKEN (nhúng role + id profile để client biết đang là Customer hay Driver)
        private string GenerateJwt(string userId, string name, string email, int regionId, string role, int? customerId, int? driverId)
        {
            // Đọc JWT key từ appsettings.json
            var jwtKey = _config["Jwt:Key"]
                ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";

            // Thông tin được nhúng vào trong token
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Name, name ?? string.Empty),
                new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
                new("regionId", regionId.ToString()),
                new("role", role ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (customerId is int cid)
                claims.Add(new Claim("customerId", cid.ToString()));
            if (driverId is int did)
                claims.Add(new Claim("driverId", did.ToString()));

            // Tạo chữ ký bảo mật cho token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Tạo token với thời hạn 1 ngày
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "RideAPI",   // ai phát hành token
                audience: _config["Jwt:Audience"] ?? "RideApp",   // token dành cho ai
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),             // hết hạn sau 1 ngày
                signingCredentials: creds
            );

            // Chuyển token thành chuỗi để trả về cho app
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // CÁC LỚP NHẬN DỮ LIỆU TỪ APP GỬI LÊN (Request DTOs)

    // Dữ liệu app gửi lên khi đăng nhập
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty; // email đăng nhập
        public string Password { get; set; } = string.Empty; // mật khẩu
    }

    // Dữ liệu app gửi lên khi đăng ký
    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty; // họ tên
        public string Phone { get; set; } = string.Empty; // số điện thoại (không bắt buộc)
        public string Email { get; set; } = string.Empty; // email đăng ký
        public string Password { get; set; } = string.Empty; // mật khẩu
    }
}
