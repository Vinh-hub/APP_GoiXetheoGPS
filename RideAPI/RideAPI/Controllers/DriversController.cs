using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RideAPI.Models;
using RideAPI.Services;

namespace RideAPI.Controllers
{
    [ApiController]
    [Route("api/drivers")]
    public class DriversController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly IWebHostEnvironment _env;

        public DriversController(DatabaseService db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        /// <summary>Cập nhật vị trí GPS của tài xế (JWT phải là tài khoản Role=Driver, có claim driverId).</summary>
        [HttpPost("update-location")]
        [Authorize]
        public async Task<IActionResult> UpdateLocation(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromBody] UpdateDriverLocationRequest request)
        {
            var driverId = GetDriverIdFromClaims();
            if (driverId is null)
                return Forbid();

            if (request is null)
                return BadRequest(new { message = "Thiếu body JSON." });

            if (!IsValidLatitude(request.Latitude) || !IsValidLongitude(request.Longitude))
                return BadRequest(new { message = "Tọa độ không hợp lệ." });

            double lat = GetLatitude(userLatitude);

            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                await using (var verify = new NpgsqlCommand(
                                 "SELECT COUNT(*) FROM Drivers WHERE DriverID = @id", conn))
                {
                    verify.Parameters.AddWithValue("@id", driverId.Value);
                    var exists = Convert.ToInt32(await verify.ExecuteScalarAsync());
                    if (exists == 0)
                        return BadRequest(new { message = "Tài xế không thuộc khu vực DB này. Kiểm tra header X-User-Latitude." });
                }

                const string sql = @"INSERT INTO DriverLocations (DriverID, Latitude, Longitude, UpdatedAt)
                                     VALUES (@driverId, @lat, @lng, CURRENT_TIMESTAMP)";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@driverId", driverId.Value);
                cmd.Parameters.AddWithValue("@lat", request.Latitude);
                cmd.Parameters.AddWithValue("@lng", request.Longitude);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    message = "Đã cập nhật vị trí.",
                    driverId = driverId.Value,
                    latitude = request.Latitude,
                    longitude = request.Longitude
                });
            }
            catch (NpgsqlException ex)
            {
                return StatusCode(503, new
                {
                    message = "Lỗi kết nối CSDL khu vực.",
                    detail = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }

        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<IActionResult> Nearby(
            [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10,
            [FromQuery] int limit = 20)
        {
            if (!IsValidLatitude(latitude) || !IsValidLongitude(longitude))
                return BadRequest(new { message = "Tham số latitude/longitude không hợp lệ." });

            if (radiusKm <= 0 || radiusKm > 500)
                return BadRequest(new { message = "radiusKm phải trong khoảng (0, 500]." });

            limit = Math.Clamp(limit, 1, 100);

            double lat = GetLatitude(userLatitude);

            try
            {
                using var conn = _db.GetConnection(lat);
                await conn.OpenAsync();

                const string sql = @"
SELECT t.DriverID,
       t.Name,
       t.Phone,
       t.Status,
       t.Latitude,
       t.Longitude,
       t.DistanceKm
FROM (
    SELECT d.DriverID,
           d.Name,
           d.Phone,
           d.Status,
           dl.Latitude,
           dl.Longitude,
           (6371 * ACOS(GREATEST(-1, LEAST(1,
               COS(RADIANS(@refLat)) * COS(RADIANS(dl.Latitude)) * COS(RADIANS(dl.Longitude) - RADIANS(@refLng))
               + SIN(RADIANS(@refLat)) * SIN(RADIANS(dl.Latitude))
           )))) AS DistanceKm
    FROM DriverLocations dl
    INNER JOIN Drivers d ON d.DriverID = dl.DriverID
    WHERE LOWER(d.IsActive::text) IN ('1', 't', 'true')
      AND dl.LocationID = (
          SELECT MAX(dl2.LocationID)
          FROM DriverLocations dl2
          WHERE dl2.DriverID = dl.DriverID
      )
) AS t
WHERE t.DistanceKm <= @radiusKm
ORDER BY t.DistanceKm ASC
LIMIT @limit";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@refLat", latitude);
                cmd.Parameters.AddWithValue("@refLng", longitude);
                cmd.Parameters.AddWithValue("@radiusKm", radiusKm);
                cmd.Parameters.AddWithValue("@limit", limit);

                var list = new List<NearbyDriverDto>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new NearbyDriverDto
                    {
                        DriverId = reader.GetInt32(reader.GetOrdinal("DriverID")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? "" : reader.GetString(reader.GetOrdinal("Phone")),
                        Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "" : reader.GetString(reader.GetOrdinal("Status")),
                        Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                        Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                        DistanceKm = Math.Round(reader.GetDouble(reader.GetOrdinal("DistanceKm")), 3)
                    });
                }

                return Ok(list);
            }
            catch (NpgsqlException ex)
            {
                return StatusCode(503, new
                {
                    message = "Lỗi kết nối CSDL khu vực.",
                    detail = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }

        private int? GetDriverIdFromClaims()
        {
            // JwtBearer MapInboundClaims mặc định: claim "role" trong token → ClaimTypes.Role.
            var role = User.FindFirst("role")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase))
                return null;

            var raw = User.FindFirst("driverId")?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }

        private double GetLatitude(double? userLatitude)
        {
            if (userLatitude is double latFromParam && IsValidLatitude(latFromParam))
                return latFromParam;

            if (Request.Headers.TryGetValue("X-User-Latitude", out var val) &&
                double.TryParse(val, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                IsValidLatitude(lat))
                return lat;

            return 10.8;
        }

        private static bool IsValidLatitude(double latitude)
            => !double.IsNaN(latitude) && !double.IsInfinity(latitude) && latitude >= -90 && latitude <= 90;

        private static bool IsValidLongitude(double longitude)
            => !double.IsNaN(longitude) && !double.IsInfinity(longitude) && longitude >= -180 && longitude <= 180;
    }
}

namespace RideAPI.Models
{
    public class UpdateDriverLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class NearbyDriverDto
    {
        public int DriverId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceKm { get; set; }
    }
}
