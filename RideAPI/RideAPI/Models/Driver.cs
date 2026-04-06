namespace RideAPI.Models
{
    public class Driver
    {
        public int DriverID { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsAvailable { get; set; }
        public int RegionID { get; set; }
    }
}