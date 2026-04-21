namespace APP_GoiXetheoGPS.Services;

public sealed class RatingApiService
{
    readonly ApiClient _api;

    public RatingApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<RatingResponse?> RateDriverAsync(int tripId, int rating, string? comment, CancellationToken cancellationToken = default)
        => _api.PostAsync<RatingRequest, RatingResponse>("/api/rating", new RatingRequest(tripId, rating, comment), true, cancellationToken);

    public sealed record RatingRequest(int TripId, int Rating, string? Comment);

    public sealed class RatingResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
