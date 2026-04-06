using RideAPI.Models;

namespace RideAPI.Services
{
    public class TripService
    {
        public object RequestTrip(TripRequestDto request)
        {
            return new
            {
                message = "Trip requested successfully",
                trip = new
                {
                    userID = request.UserID,
                    latitude = request.Latitude,
                    longitude = request.Longitude,
                    pickupAddress = request.PickupAddress,
                    destinationAddress = request.DestinationAddress,
                    price = request.Price,
                    status = "Requested"
                }
            };
        }

        public object GetTrip(int id, double latitude)
        {
            return new
            {
                tripID = id,
                latitude = latitude,
                status = "Requested",
                message = "Trip details fetched successfully"
            };
        }

        public object AcceptTrip(AcceptTripDto request)
        {
            return new
            {
                message = "Trip accepted successfully",
                tripID = request.TripID,
                driverID = request.DriverID,
                status = "Accepted"
            };
        }

        public object CompleteTrip(CompleteTripDto request)
        {
            return new
            {
                message = "Trip completed successfully",
                tripID = request.TripID,
                finalPrice = request.FinalPrice,
                status = "Completed"
            };
        }
    }
}