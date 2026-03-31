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
    [ApiController]      // đánh dấu đây là API Controller, .NET sẽ tự xử lý validate request
    public class AuthController : ControllerBase
    {
        private readonly DatabaseService _db;       // dùng để kết nối tới NorthDB hoặc SouthDB
        private readonly IConfiguration _config;    // dùng để đọc cấu hình từ appsettings.json

        // ── Constructor: .NET tự inject DatabaseService và IConfiguration vào ──
        public AuthController(DatabaseService db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // API 1: ĐĂNG NHẬP
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Bước 1: Kiểm tra người dùng có nhập đủ thông tin chưa
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email và mật khẩu không được để trống." });

            // Bước 2: Đọc vĩ độ từ header để xác định kết nối DB nào
            //   > 16  → NorthDB (Hà Nội và các tỉnh miền Bắc)
            //   <= 16 → SouthDB (TP.HCM và các tỉnh miền Nam)
            double lat = GetLatitude();

            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                // Bước 4: Tìm user trong DB theo email và password
                var sql = @"SELECT UserID, Name, Email, RegionID, IsActive
                            FROM Users
                            WHERE Email = @email AND Password = @pwd
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

                // Bước 7: Lấy thông tin user từ DB
                var user = new User
                {
                    UserID = reader.GetInt32("UserID"),
                    Name = reader.GetString("Name"),
                    Email = reader.GetString("Email"),
                    RegionID = reader.GetInt32("RegionID"),
                    IsActive = isActive
                };

                // Bước 8: Tạo JWT token chứa thông tin user
                var token = GenerateJwt(user.UserID.ToString(), user.Name, user.Email, user.RegionID);

                // Bước 9: Trả về token và thông tin user cho app
                return Ok(new
                {
                    message = "Đăng nhập thành công.",
                    token,                      // JWT token, app lưu lại dùng cho các request sau
                    userId = user.UserID,
                    name = user.Name,
                    email = user.Email,
                    regionId = user.RegionID    // 1 = Bắc, 2 = Nam
                });
            }
            catch (MySqlException)
            {
                return StatusCode(503, new { message = "Khu vực này đang bảo trì. Vui lòng thử lại sau." });
            }
        }

        // API 2: ĐĂNG KÝ TÀI KHOẢN MỚI
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Bước 1: Kiểm tra người dùng có nhập đủ thông tin bắt buộc chưa
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });

            // Bước 2: Đọc vĩ độ từ header → xác định user thuộc vùng nào
            //   > 16  → RegionID = 1 (Miền Bắc) → lưu vào NorthDB
            //   <= 16 → RegionID = 2 (Miền Nam)  → lưu vào SouthDB
            double lat = GetLatitude();
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

                // Bước 5: Thêm user mới vào DB
                //   IsActive mặc định = 1 (tài khoản hoạt động bình thường)
                var insertSql = @"INSERT INTO Users (Name, Phone, Email, Password, RegionID, IsActive)
                                  VALUES (@name, @phone, @email, @pwd, @region, 1)";
                using var insertCmd = new MySqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@name", request.Name.Trim());
                insertCmd.Parameters.AddWithValue("@phone", request.Phone?.Trim() ?? "");
                insertCmd.Parameters.AddWithValue("@email", request.Email.Trim());
                insertCmd.Parameters.AddWithValue("@pwd", request.Password);
                insertCmd.Parameters.AddWithValue("@region", regionId);

                await insertCmd.ExecuteNonQueryAsync();

                // Bước 6: Lấy ID vừa được tạo trong DB
                var newId = (int)insertCmd.LastInsertedId;

                // Bước 7: Tạo JWT token cho user mới
                var token = GenerateJwt(newId.ToString(), request.Name, request.Email, regionId);

                // Bước 8: Trả về token và thông tin user cho app
                return Ok(new
                {
                    message = "Đăng ký thành công.",
                    token,
                    userId = newId,
                    name = request.Name,
                    email = request.Email,
                    regionId
                });
            }
            catch (MySqlException)
            {
                return StatusCode(503, new { message = "Khu vực này đang bảo trì. Vui lòng thử lại sau." });
            }
        }

        // HELPER 1: LẤY VĨ ĐỘ TỪ HEADER
        // Đọc header "X-User-Latitude" từ request gửi lên
        // Nếu không có header → dùng mặc định 10.8 (TP.HCM, miền Nam)
        private double GetLatitude()
        {
            if (Request.Headers.TryGetValue("X-User-Latitude", out var val) &&
                double.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var lat))
                return lat;

            return 10.8;
        }

        // HELPER 2: TẠO JWT TOKEN
        private string GenerateJwt(string userId, string name, string email, int regionId)
        {
            // Đọc JWT key từ appsettings.json
            var jwtKey = _config["Jwt:Key"]
                ?? "YourSuperSecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";

            // Thông tin được nhúng vào trong token
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   userId),    // ID người dùng
                new Claim(JwtRegisteredClaimNames.Name,  name),      // Tên người dùng
                new Claim(JwtRegisteredClaimNames.Email, email),     // Email
                new Claim("regionId",                    regionId.ToString()), // Vùng (1=Bắc, 2=Nam)
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()) // ID duy nhất của token
            };

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
