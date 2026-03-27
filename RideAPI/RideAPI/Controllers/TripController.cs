using Microsoft.AspNetCore.Mvc;
using RideAPI.Models;
using RideAPI.Services;

namespace RideAPI.Controllers
{
    [ApiController]
    [Route("api/trips")]
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