namespace RideAPI.Models.ViewModels
{
    public class BookRideViewModel
    {
        public int DriverId { get; set; }
        public decimal Price { get; set; } = 50000;
        public double StartLat { get; set; } = 21.0245;
        public double StartLng { get; set; } = 105.8510;
        public double EndLat { get; set; } = 21.2181;
        public double EndLng { get; set; } = 105.7898;
    }
}
