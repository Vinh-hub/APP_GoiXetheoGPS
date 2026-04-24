using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideAPI.Models;
using RideAPI.Services;

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

        [HttpPost]
        public async Task<IActionResult> BookRide([FromBody] BookRideRequest request)
        {
            var region = LocationRoutingService.ResolveRegion(request.StartLat, request.Province);

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "Token không hợp lệ" });

            try
            {
                var tripId = await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    const string sql = @"
                        INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, CreatedAt)
                        VALUES (@UserId, @DriverId, 'Requested', @Price, @StartLat, @StartLng, @EndLat, @EndLng, NOW())
                        RETURNING TripID;";

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
                }, region, true);

                return Ok(new { tripId, message = "Đặt chuyến thành công" });
            }
            catch (InvalidOperationException ex) when (ex.Message == "MASTER_DOWN_CANNOT_WRITE")
            {
                return StatusCode(503, new { error = "Hệ thống đang ở chế độ chỉ đọc, không thể đặt chuyến" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] double? latitude, [FromQuery] string? province)
        {
            var region = LocationRoutingService.ResolveRegion(latitude, province);

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            try
            {
                var trips = await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    const string sql = @"
                        SELECT TripID, UserID, DriverID, Status, Price,
                               StartLat, StartLng, EndLat, EndLng,
                               PaymentAmount, DriverRating, DriverComment, CreatedAt
                        FROM Trips
                        WHERE UserID = @UserId
                        ORDER BY CreatedAt DESC";

                    return (await conn.QueryAsync<Trip>(sql, new { UserId = userId })).ToList();
                }, region, false);

                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy lịch sử: " + ex.Message });
            }
        }
    }

    public class BookRideRequest
    {
        public int DriverId { get; set; }
        public decimal Price { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
        public string? Province { get; set; }
    }
}