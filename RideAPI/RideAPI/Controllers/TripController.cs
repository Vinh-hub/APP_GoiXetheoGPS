using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
        public IActionResult RequestTrip([FromBody] TripRequestDto request)
        {
            _tripService.RequestTrip(request);
            return Ok("Trip created");
        }

        [HttpGet("{id}")]
        public IActionResult GetTrip(int id, [FromQuery] double latitude)
        {
            var trip = _tripService.GetTrip(id, latitude);

            if (trip == null)
                return NotFound("Trip not found");

            return Ok(trip);
        }

        [HttpPost("accept")]
        public IActionResult AcceptTrip([FromBody] AcceptTripDto request)
        {
            _tripService.AcceptTrip(request);
            return Ok("Trip accepted");
        }

        [HttpPost("complete")]
        public IActionResult CompleteTrip([FromBody] CompleteTripDto request)
        {
            _tripService.CompleteTrip(request);
            return Ok("Trip completed");
        }
    }
}