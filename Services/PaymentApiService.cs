namespace APP_GoiXetheoGPS.Services;

public sealed class PaymentApiService
{
    readonly ApiClient _api;

    public PaymentApiService(ApiClient api)
    {
        _api = api;
    }

    public Task<PaymentResponse?> ProcessPaymentAsync(int tripId, decimal amount, CancellationToken cancellationToken = default)
        => _api.PostAsync<PaymentRequest, PaymentResponse>("/api/payment", new PaymentRequest(tripId, amount), true, cancellationToken);

    public sealed record PaymentRequest(int TripId, decimal Amount);

    public sealed class PaymentResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
