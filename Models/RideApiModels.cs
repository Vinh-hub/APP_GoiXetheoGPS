namespace APP_GoiXetheoGPS.Models;

public sealed class CreateRideRequest
{
    public int DriverId { get; init; }
    public decimal Price { get; init; }
    public double StartLat { get; init; }
    public double StartLng { get; init; }
    public double EndLat { get; init; }
    public double EndLng { get; init; }
}

public sealed class CreateRideResponse
{
    public int TripId { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class RideHistoryDto
{
    public int TripId { get; init; }
    public int UserId { get; init; }
    public int DriverId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public double? StartLat { get; init; }
    public double? StartLng { get; init; }
    public double? EndLat { get; init; }
    public double? EndLng { get; init; }
    public decimal? PaymentAmount { get; init; }
    public int? DriverRating { get; init; }
    public string DriverComment { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
