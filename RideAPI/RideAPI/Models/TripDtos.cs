namespace RideAPI.Models
{
    public class TripRequestDto
    {
        public int UserID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PickupAddress { get; set; } = string.Empty;
        public string DestinationAddress { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class AcceptTripDto
    {
        public int TripID { get; set; }
        public int DriverID { get; set; }
        public double Latitude { get; set; }
    }

    public class CompleteTripDto
    {
        public int TripID { get; set; }
        public decimal FinalPrice { get; set; }
        public double Latitude { get; set; }
    }
}