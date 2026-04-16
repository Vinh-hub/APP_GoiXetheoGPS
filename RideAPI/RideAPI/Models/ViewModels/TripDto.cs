namespace RideAPI.Models.ViewModels
{
    public class TripDto
    {
        public int TripId { get; set; }
        public int UserId { get; set; }
        public int DriverId { get; set; }
        public string Status { get; set; }
        public decimal Price { get; set; }
        public double? StartLat { get; set; }
        public double? StartLng { get; set; }
        public double? EndLat { get; set; }
        public double? EndLng { get; set; }
        public decimal? PaymentAmount { get; set; }
        public int? DriverRating { get; set; }
        public string DriverComment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}