using System.Globalization;

namespace APP_GoiXetheoGPS.Models;

public sealed class NearbyDriverItem
{
    public int DriverId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double DistanceKm { get; init; }

    public string StatusDisplay =>
        string.IsNullOrWhiteSpace(Status) ? "Sẵn sàng nhận chuyến" : Status.Trim();

    public string DistanceDisplay =>
        $"{DistanceKm.ToString("0.0", CultureInfo.InvariantCulture)} km";

    public string Summary => $"{Name} • {DistanceDisplay}";
}
