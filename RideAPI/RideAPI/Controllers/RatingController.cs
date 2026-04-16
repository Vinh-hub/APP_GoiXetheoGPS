using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using RideAPI.Services;
using System.Security.Claims;

namespace RideAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RatingController : ControllerBase
    {
        private readonly DbRetryService _dbRetry;

        public RatingController(DbRetryService dbRetry)
        {
            _dbRetry = dbRetry;
        }

        [HttpPost]
        public async Task<IActionResult> RateDriver([FromBody] RatingRequest request)
        {
            var region = HttpContext.Items["Region"] as string;
            if (string.IsNullOrEmpty(region))
                return BadRequest(new { error = "Không xác định được khu vực" });

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            // Validate rating
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { error = "Đánh giá phải từ 1 đến 5 sao" });

            try
            {
                await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    // Kiểm tra chuyến đi đã hoàn thành và chưa đánh giá
                    var checkSql = @"
                        SELECT COUNT(*) FROM Trips 
                        WHERE TripID = @TripId AND UserID = @UserId 
                        AND Status = 'Paid' AND DriverRating IS NULL";
                    var count = await conn.ExecuteScalarAsync<int>(checkSql, new { request.TripId, UserId = userId });
                    if (count == 0)
                        throw new Exception("INVALID_RATING");

                    var updateSql = @"
                        UPDATE Trips 
                        SET DriverRating = @Rating, DriverComment = @Comment 
                        WHERE TripID = @TripId";
                    return await conn.ExecuteAsync(updateSql, new { request.Rating, request.Comment, request.TripId });
                }, region, true);

                // Cập nhật lại AvgRating cho driver (tính trung bình)
                await UpdateDriverAverageRating(request.TripId, region);

                return Ok(new { success = true, message = "Cảm ơn bạn đã đánh giá" });
            }
            catch (Exception ex) when (ex.Message == "MASTER_DOWN_CANNOT_WRITE")
            {
                return StatusCode(503, new { error = "Đánh giá tạm thời không khả dụng (hệ thống chỉ đọc)" });
            }
            catch (Exception ex) when (ex.Message == "INVALID_RATING")
            {
                return BadRequest(new { error = "Chuyến đi không hợp lệ hoặc đã được đánh giá" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lưu đánh giá: " + ex.Message });
            }
        }

        private async Task UpdateDriverAverageRating(int tripId, string region)
        {
            try
            {
                await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    // Lấy driverId từ trip
                    var driverId = await conn.ExecuteScalarAsync<int?>("SELECT DriverID FROM Trips WHERE TripID = @TripId", new { TripId = tripId });
                    if (driverId == null) return 0;

                    // Tính avg rating mới
                    var avgSql = @"
                        UPDATE Drivers d
                        SET d.AvgRating = (
                            SELECT COALESCE(AVG(DriverRating), 0) 
                            FROM Trips 
                            WHERE DriverID = d.DriverID AND DriverRating IS NOT NULL
                        )
                        WHERE d.DriverID = @DriverId";
                    return await conn.ExecuteAsync(avgSql, new { DriverId = driverId });
                }, region, true);
            }
            catch { /* bỏ qua lỗi khi cập nhật avg, không ảnh hưởng đến rating chính */ }
        }
    }

    public class RatingRequest
    {
        public int TripId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}