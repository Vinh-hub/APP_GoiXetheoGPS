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

    [Range(-90, 90, ErrorMessage = "Latitude phải trong khoảng -90 đến 90")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude phải trong khoảng -180 đến 180")]
    public double? Longitude { get; set; }

    public string Province { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int RegionId { get; set; }
}
