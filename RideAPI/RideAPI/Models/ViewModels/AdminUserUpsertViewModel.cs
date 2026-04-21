using System.ComponentModel.DataAnnotations;

namespace RideAPI.Models.ViewModels;

public class AdminUserUpsertViewModel
{
    public int? UserId { get; set; }

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role là bắt buộc")]
    public string Role { get; set; } = "Customer";

    public int? CustomerId { get; set; }
    public int? DriverId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int RegionId { get; set; }
}
