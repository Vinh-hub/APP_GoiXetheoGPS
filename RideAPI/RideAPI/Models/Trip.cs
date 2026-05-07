namespace RideAPI.Models
{
    public class Trip
    {
        public int TripID { get; set; }
        public int UserID { get; set; }
        public int DriverID { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public double? StartLat { get; set; }
        public double? StartLng { get; set; }
        public double? EndLat { get; set; }
        public double? EndLng { get; set; }
        public decimal? PaymentAmount { get; set; }
        public int? DriverRating { get; set; }
        public string? DriverComment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}