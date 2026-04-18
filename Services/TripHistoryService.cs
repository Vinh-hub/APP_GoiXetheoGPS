using System.Globalization;
using APP_GoiXetheoGPS.Models;

namespace APP_GoiXetheoGPS.Services;

public sealed class TripHistoryService
{
    private readonly AppSettingsService _settingsService;
    private readonly RideApiService _rideApiService;

    public TripHistoryService(AppSettingsService settingsService, RideApiService rideApiService)
    {
        _settingsService = settingsService;
        _rideApiService = rideApiService;
    }

    public async Task<TripHistoryLoadResult> GetPreferredHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_settingsService.HasApiBaseUrlConfigured() && _settingsService.HasJwtConfigured())
        {
            try
            {
                var apiTrips = await _rideApiService.GetRideHistoryAsync(cancellationToken);
                var mappedTrips = apiTrips
                    .Select(MapApiTrip)
                    .OrderByDescending(x => x.WhenLocal)
                    .ToList();

                return new TripHistoryLoadResult(
                    mappedTrips.GroupBy(x => x.MonthGroupTitle)
                        .OrderByDescending(x => x.Max(item => item.WhenLocal))
                        .Select(group => new TripMonthGroup(group.Key, group.OrderByDescending(item => item.WhenLocal)))
                        .ToList(),
                    mappedTrips,
                    "Đang xem dữ liệu chuyến từ server.",
                    false);
            }
            catch (Exception ex)
            {
                var fallback = await LoadLocalFallbackAsync(cancellationToken);
                return fallback with
                {
                    SourceMessage = $"Không lấy được lịch sử từ server, đang dùng dữ liệu mẫu local. {SimplifyMessage(ex.Message)}",
                    IsFallback = true
                };
            }
        }

        var local = await LoadLocalFallbackAsync(cancellationToken);
        return local with
        {
            SourceMessage = "Chưa cấu hình API/JWT, đang dùng dữ liệu mẫu local để demo.",
            IsFallback = true
        };
    }

    public async Task<TripHistoryItem?> FindTripByIdAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var history = await GetPreferredHistoryAsync(cancellationToken);
        return history.Trips.FirstOrDefault(x => x.Id == id);
    }

    private static async Task<TripHistoryLoadResult> LoadLocalFallbackAsync(CancellationToken cancellationToken)
    {
        var localTrips = await TripDataStore.GetAllTripsAsync(cancellationToken);
        var orderedTrips = localTrips.OrderByDescending(x => x.WhenLocal).ToList();
        var groups = orderedTrips
            .GroupBy(x => x.MonthGroupTitle)
            .OrderByDescending(x => x.Max(item => item.WhenLocal))
            .Select(group => new TripMonthGroup(group.Key, group.OrderByDescending(item => item.WhenLocal)))
            .ToList();

        return new TripHistoryLoadResult(groups, orderedTrips, "Đang xem dữ liệu mẫu local.", true);
    }

    private static TripHistoryItem MapApiTrip(RideHistoryDto dto)
    {
        var whenLocal = dto.CreatedAt.Kind switch
        {
            DateTimeKind.Utc => dto.CreatedAt.ToLocalTime(),
            _ => dto.CreatedAt
        };

        var price = dto.PaymentAmount ?? dto.Price;

        return new TripHistoryItem
        {
            Id = dto.TripId.ToString(CultureInfo.InvariantCulture),
            From = FormatCoordinateLabel("Điểm đón", dto.StartLat, dto.StartLng),
            To = FormatCoordinateLabel("Điểm đến", dto.EndLat, dto.EndLng),
            WhenLocal = whenLocal,
            DriverName = dto.DriverId > 0 ? $"Tài xế #{dto.DriverId}" : "Chưa gán tài xế",
            VehicleInfo = dto.DriverRating.HasValue
                ? $"Đánh giá tài xế: {dto.DriverRating}/5"
                : "Dữ liệu phương tiện từ API chưa có",
            PriceVnd = price,
            Status = MapStatus(dto.Status)
        };
    }

    private static string FormatCoordinateLabel(string prefix, double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            return $"{prefix} chưa có dữ liệu";

        return $"{prefix} {latitude.Value.ToString("0.0000", CultureInfo.InvariantCulture)}, {longitude.Value.ToString("0.0000", CultureInfo.InvariantCulture)}";
    }

    private static string MapStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "requested" => "Đã yêu cầu",
            "accepted" => "Đã nhận",
            "inprogress" => "Đang chạy",
            "completed" => "Hoàn thành",
            "cancelled" => "Đã hủy",
            _ when string.IsNullOrWhiteSpace(status) => "Chưa rõ trạng thái",
            _ => status!.Trim()
        };
    }

    private static string SimplifyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message.EndsWith('.') ? message : $"{message}.";
    }
}

public sealed record TripHistoryLoadResult(
    IReadOnlyList<TripMonthGroup> Groups,
    IReadOnlyList<TripHistoryItem> Trips,
    string SourceMessage,
    bool IsFallback);
