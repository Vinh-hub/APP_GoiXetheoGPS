namespace RideAPI.Models
{
    public class Trip
    {
        public int TripID { get; set; }
        public int UserID { get; set; }
        public int DriverID { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public double Latitude { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
