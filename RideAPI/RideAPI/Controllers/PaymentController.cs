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
    public class PaymentController : ControllerBase
    {
        private readonly DbRetryService _dbRetry;

        public PaymentController(DbRetryService dbRetry)
        {
            _dbRetry = dbRetry;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
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
                await _dbRetry.ExecuteWithRetry(async conn =>
                {
                    // Kiểm tra chuyến đi có thuộc user không và chưa thanh toán
                    var checkSql = "SELECT COUNT(*) FROM Trips WHERE TripID = @TripId AND UserID = @UserId AND PaymentAmount IS NULL";
                    var count = await conn.ExecuteScalarAsync<int>(checkSql, new { request.TripId, UserId = userId });
                    if (count == 0)
                        throw new Exception("INVALID_TRIP");

                    var updateSql = "UPDATE Trips SET Status = 'Paid', PaymentAmount = @Amount WHERE TripID = @TripId";
                    return await conn.ExecuteAsync(updateSql, new { request.Amount, request.TripId });
                }, region, true);

                return Ok(new { success = true, message = "Thanh toán thành công" });
            }
            catch (Exception ex) when (ex.Message == "MASTER_DOWN_CANNOT_WRITE")
            {
                return StatusCode(503, new { error = "Thanh toán tạm thời không khả dụng (hệ thống chỉ đọc)" });
            }
            catch (Exception ex) when (ex.Message == "INVALID_TRIP")
            {
                return BadRequest(new { error = "Chuyến đi không hợp lệ hoặc đã được thanh toán" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi xử lý thanh toán: " + ex.Message });
            }
        }
    }

    public class PaymentRequest
    {
        public int TripId { get; set; }
        public decimal Amount { get; set; }
    }
}