namespace RideAPI.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RegionID { get; set; }
        public bool IsActive { get; set; } = true;   // true = hoạt động, false = bị khóa
        public DateTime CreatedAt { get; set; }
    }
}
