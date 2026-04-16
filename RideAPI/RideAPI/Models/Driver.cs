namespace RideAPI.Models
{
    public class Driver
    {
        public int DriverID { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
        public int RegionID { get; set; }
        public bool IsActive { get; set; }
        public float AvgRating { get; set; }
    }
}