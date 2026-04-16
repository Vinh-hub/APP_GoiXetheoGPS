namespace RideAPI.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        public int? CustomerID { get; set; }
        public int? DriverID { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public int RegionID { get; set; }
        public bool IsActive { get; set; }
    }
}