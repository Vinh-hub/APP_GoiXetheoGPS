using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideAPI.Models;
using RideAPI.Services;

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
            var tripId = await _tripService.RequestTripAsync(request);
            return Ok(new { tripId, message = "Trip created" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTrip(int id, [FromQuery] double latitude)
        {
            var trip = await _tripService.GetTripAsync(id, latitude);
            if (trip == null)
                return NotFound("Trip not found");

            return Ok(trip);
        }

        [HttpPost("accept")]
        public async Task<IActionResult> AcceptTrip([FromBody] AcceptTripDto request)
        {
            await _tripService.AcceptTripAsync(request);
            return Ok("Trip accepted");
        }

        [HttpPost("complete")]
        public async Task<IActionResult> CompleteTrip([FromBody] CompleteTripDto request)
        {
            await _tripService.CompleteTripAsync(request);
            return Ok("Trip completed");
        }
    }
}