using System.Text.Json.Serialization;

namespace APP_GoiXetheoGPS.Services;

public sealed class RideApiService
{
    readonly ApiClient _api;

    public RideApiService(ApiClient api)
    {
        _api = api;
    }

    public async Task<BookRideResponse?> BookRideAsync(BookRideRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _api.PostAsync<BookRideRequest, BookRideResponse>("/api/rides", request, true, cancellationToken);
        TripDataStore.InvalidateCache();
        return response;
    }

    public async Task<IReadOnlyList<RideHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var result = await _api.GetAsync<List<RideHistoryItem>>("/api/rides/history", true, cancellationToken);
        if (result is null)
            return Array.Empty<RideHistoryItem>();

        return result;
    }

    public sealed class BookRideRequest
    {
        public int DriverId { get; set; }
        public decimal Price { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
    }

    public sealed class BookRideResponse
    {
        public int TripId { get; set; }
        public string? Message { get; set; }
    }

    public sealed class RideHistoryItem
    {
        [JsonPropertyName("TripID")]
        public int TripId { get; set; }

        [JsonPropertyName("UserID")]
        public int UserId { get; set; }

        [JsonPropertyName("DriverID")]
        public int DriverId { get; set; }

        [JsonPropertyName("Status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("Price")]
        public decimal Price { get; set; }

        [JsonPropertyName("StartLat")]
        public double StartLat { get; set; }

        [JsonPropertyName("StartLng")]
        public double StartLng { get; set; }

        [JsonPropertyName("EndLat")]
        public double EndLat { get; set; }

        [JsonPropertyName("EndLng")]
        public double EndLng { get; set; }

        [JsonPropertyName("PaymentAmount")]
        public decimal? PaymentAmount { get; set; }

        [JsonPropertyName("DriverRating")]
        public int? DriverRating { get; set; }

        [JsonPropertyName("DriverComment")]
        public string? DriverComment { get; set; }

        [JsonPropertyName("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}
