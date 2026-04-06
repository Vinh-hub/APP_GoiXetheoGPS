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
            var result = _tripService.RequestTrip(request);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public IActionResult GetTrip(int id, [FromQuery] double latitude)
        {
            var result = _tripService.GetTrip(id, latitude);
            return Ok(result);
        }

        [HttpPost("accept")]
        public IActionResult AcceptTrip([FromBody] AcceptTripDto request)
        {
            var result = _tripService.AcceptTrip(request);
            return Ok(result);
        }

        [HttpPost("complete")]
        public IActionResult CompleteTrip([FromBody] CompleteTripDto request)
        {
            var result = _tripService.CompleteTrip(request);
            return Ok(result);
        }
    }
}