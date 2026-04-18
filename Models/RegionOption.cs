namespace APP_GoiXetheoGPS.Models;

public sealed class RegionOption
{
    public required string Key { get; init; }
    public required string CityName { get; init; }
    public required string RoutingBucket { get; init; }
    public required double SampleLatitude { get; init; }
    public required double SampleLongitude { get; init; }

    public string DisplayName => $"{CityName} ({RoutingBucket})";
}
