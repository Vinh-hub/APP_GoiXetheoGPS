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
        TripService service = new TripService();

        [HttpPost]
        public IActionResult BookTrip(Trip trip)
        {
            service.BookTrip(trip);
            return Ok("Trip created");
        }
    }
}