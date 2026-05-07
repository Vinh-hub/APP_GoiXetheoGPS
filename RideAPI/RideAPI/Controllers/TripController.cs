using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideAPI.Models;
using RideAPI.Services;
using System.Security.Claims;

namespace RideAPI.Controllers
{
    [ApiController]
    [Route("api/trips")]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly TripService _tripService;

        public TripController(TripService tripService)
        {
            _tripService = tripService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestTrip([FromBody] TripRequestDto request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var role = GetRole();
            if (!string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var tripId = await _tripService.RequestTripAsync(request, userId);
            return Ok(new { tripId, message = "Trip created" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTrip(int id, [FromQuery] double latitude)
        {
            var trip = await _tripService.GetTripAsync(id, latitude);
            if (trip == null)
                return NotFound("Trip not found");

            if (!CanReadTrip(trip))
                return Forbid();

            return Ok(trip);
        }

        [HttpPost("accept")]
        public async Task<IActionResult> AcceptTrip([FromBody] AcceptTripDto request)
        {
            if (!TryGetDriverId(out var driverId))
                return Forbid();

            await _tripService.AcceptTripAsync(request, driverId);
            return Ok("Trip accepted");
        }

        [HttpPost("complete")]
        public async Task<IActionResult> CompleteTrip([FromBody] CompleteTripDto request)
        {
            await _tripService.CompleteTripAsync(request);
            return Ok("Trip completed");
        }

        private bool TryGetUserId(out int userId)
            => int.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

        private bool TryGetDriverId(out int driverId)
        {
            driverId = 0;
            if (!string.Equals(GetRole(), "Driver", StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(User.FindFirst("driverId")?.Value, out driverId);
        }

        private string GetRole()
            => User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        private bool CanReadTrip(Trip trip)
        {
            var role = GetRole();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase)
                && TryGetUserId(out var userId)
                && trip.UserID == userId)
                return true;

            return string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(User.FindFirst("driverId")?.Value, out var driverId)
                   && trip.DriverID == driverId;
        }
    }
}