using System.Globalization;

namespace APP_GoiXetheoGPS.Services;

public sealed class DriverApiService
{
    readonly ApiClient _api;

    public DriverApiService(ApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<NearbyDriverDto>> GetNearbyDriversAsync(
        double latitude,
        double longitude,
        double radiusKm = 10,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var route = $"/api/drivers/nearby?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                    $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                    $"&radiusKm={radiusKm.ToString(CultureInfo.InvariantCulture)}&limit={limit}";

        var result = await _api.GetAsync<List<NearbyDriverDto>>(route, requiresAuth: false, cancellationToken);
        if (result is null)
            return Array.Empty<NearbyDriverDto>();

        return result;
    }

    public Task UpdateLocationAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        => _api.PostAsync("/api/drivers/update-location", new UpdateDriverLocationRequest(latitude, longitude), true, cancellationToken);

    public sealed record UpdateDriverLocationRequest(double Latitude, double Longitude);

    public sealed class NearbyDriverDto
    {
        public int DriverId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceKm { get; set; }
    }
}
