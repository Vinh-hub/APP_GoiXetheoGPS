namespace RideAPI.Models.ViewModels;

public class AdminDashboardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public int ScopedRegionId { get; set; }
    public string ScopedRegionText { get; set; } = string.Empty;
    public string ScopeLatitude { get; set; } = string.Empty;
    public string ScopeProvince { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
}
