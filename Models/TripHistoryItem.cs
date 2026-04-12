using System.Globalization;

namespace APP_GoiXetheoGPS.Models;

public sealed class TripHistoryItem
{
    public required string Id { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public DateTime WhenLocal { get; init; }
    public required string DriverName { get; init; }
    public required string VehicleInfo { get; init; }
    public decimal PriceVnd { get; init; }
    public required string Status { get; init; }

    public string RouteLine => $"Từ {From} đến {To}";

    public string WhenDisplay =>
        WhenLocal.ToString("d MMMM yyyy h:mm tt", new CultureInfo("vi-VN"));

    public string MonthGroupTitle =>
        WhenLocal.ToString("MMMM yyyy", new CultureInfo("vi-VN"));
}

public sealed class TripMonthGroup : List<TripHistoryItem>
{
    public TripMonthGroup(string monthTitle, IEnumerable<TripHistoryItem> trips)
        : base(trips)
    {
        MonthTitle = monthTitle;
    }

    public string MonthTitle { get; }
}
