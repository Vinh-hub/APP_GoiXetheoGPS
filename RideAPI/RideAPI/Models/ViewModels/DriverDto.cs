namespace RideAPI.Models.ViewModels
{
    public class DriverDto
    {
        public int DriverId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public float AvgRating { get; set; }
    }
}