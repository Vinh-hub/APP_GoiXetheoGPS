using System.Globalization;
using System.Linq;

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

        var result = await _api.GetAsync<List<NearbyDriverDto>>(route, requiresAuth: true, cancellationToken);
        if (result is null)
            return Array.Empty<NearbyDriverDto>();

        return result;
    }

    /// <summary>Chọn một tài xế đang hoạt động ngẫu nhiên trong CSDL miền (theo X-User-Latitude / query), không lọc theo bán kính.</summary>
    public async Task<NearbyDriverDto?> GetRandomDriverForBookingAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var route = $"/api/drivers/random-for-booking?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                    $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}";

        var list = await _api.GetAsync<List<NearbyDriverDto>>(route, requiresAuth: true, cancellationToken);
        return list?.FirstOrDefault();
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
