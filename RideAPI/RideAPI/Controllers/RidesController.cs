using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using RideAPI.Services;
using System.Security.Claims;
using RideAPI.Models;

namespace RideAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RidesController : ControllerBase
    {
        private readonly DbRetryService _dbRetry;

        public RidesController(DbRetryService dbRetry)
        {
            _dbRetry = dbRetry;
        }

        // POST: api/rides
        [HttpPost]
        public async Task<IActionResult> BookRide(
    [FromHeader(Name = "X-User-Latitude")] double? userLatitude,
    [FromBody] BookRideRequest request)
        {
            // Xác định region từ header (ưu tiên hơn middleware)
            string region = userLatitude.HasValue && userLatitude.Value > 16 ? "NORTH" : "SOUTH";
            if (string.IsNullOrEmpty(region))
                region = HttpContext.Items["Region"] as string; // fallback middleware

            if (string.IsNullOrEmpty(region))
                return BadRequest(new { error = "Không xác định được khu vực" });

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { error = "Token không hợp lệ" });

            int userId = int.Parse(userIdClaim);

            try
            {
                var tripId = await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    var sql = @"
                        INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, CreatedAt)
                        VALUES (@UserId, @DriverId, 'Requested', @Price, @StartLat, @StartLng, @EndLat, @EndLng, NOW());
                        SELECT LAST_INSERT_ID();";
                    return await conn.ExecuteScalarAsync<int>(sql, new
                    {
                        UserId = userId,
                        request.DriverId,
                        request.Price,
                        request.StartLat,
                        request.StartLng,
                        request.EndLat,
                        request.EndLng
                    });
                }, region, true); // write operation

                return Ok(new { tripId, message = "Đặt chuyến thành công" });
            }
            catch (Exception ex) when (ex.Message == "MASTER_DOWN_CANNOT_WRITE")
            {
                return StatusCode(503, new { error = "Hệ thống đang ở chế độ chỉ đọc, không thể đặt chuyến" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }

        // GET: api/rides/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var region = HttpContext.Items["Region"] as string;
            if (string.IsNullOrEmpty(region))
                return BadRequest(new { error = "Không xác định được khu vực" });

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            try
            {
                var trips = await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    var sql = @"
                        SELECT TripID, UserID, DriverID, Status, Price, 
                               StartLat, StartLng, EndLat, EndLng,
                               PaymentAmount, DriverRating, DriverComment, CreatedAt
                        FROM Trips
                        WHERE UserID = @UserId
                        ORDER BY CreatedAt DESC";
                    return await conn.QueryAsync<Trip>(sql, new { UserId = userId });
                }, region, false); // read operation

                return Ok(trips);
            }
            catch (Exception ex)
            {   
                return StatusCode(500, new { error = "Lỗi khi lấy lịch sử: " + ex.Message });
            }
        }
    }

    // DTO
    public class BookRideRequest
    {
        public int DriverId { get; set; }
        public decimal Price { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
    }

}