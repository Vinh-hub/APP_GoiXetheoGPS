namespace RideAPI.Models.ViewModels;

public class AdminUserListItemViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? DriverId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? RegionId { get; set; }
}
