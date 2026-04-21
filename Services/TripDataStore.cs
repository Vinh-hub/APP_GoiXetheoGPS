using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using APP_GoiXetheoGPS.Models;

namespace APP_GoiXetheoGPS.Services;

/// <summary>
/// Đọc danh sách chuyến từ Web API để đồng bộ dữ liệu với web server.
/// </summary>
public static class TripDataStore
{
    static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    static readonly AuthSessionService Session = new();
    static readonly UserLocationService Location = new();

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    static readonly SemaphoreSlim LoadGate = new(1, 1);
    static IReadOnlyList<TripHistoryItem>? _cache;

    sealed class TripRowDto
    {
        [JsonPropertyName("TripID")]
        public int? TripId { get; set; }

        [JsonPropertyName("DriverID")]
        public int? DriverId { get; set; }

        [JsonPropertyName("StartLat")]
        public double? StartLat { get; set; }

        [JsonPropertyName("StartLng")]
        public double? StartLng { get; set; }

        [JsonPropertyName("EndLat")]
        public double? EndLat { get; set; }

        [JsonPropertyName("EndLng")]
        public double? EndLng { get; set; }

        [JsonPropertyName("Price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("PaymentAmount")]
        public decimal? PaymentAmount { get; set; }

        [JsonPropertyName("CreatedAt")]
        public DateTime? CreatedAt { get; set; }

        public string? Id { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? WhenLocal { get; set; }
        public string? DriverName { get; set; }
        public string? VehicleInfo { get; set; }
        public decimal PriceVnd { get; set; }
        public string? Status { get; set; }

        [JsonPropertyName("sqlNote")]
        public string? SqlNote { get; set; }
    }

    sealed class TripListResponseDto
    {
        public List<TripRowDto>? Trips { get; set; }
    }

    public static async Task<IReadOnlyList<TripHistoryItem>> GetAllTripsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (forceRefresh)
            _cache = null;

        await LoadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache is not null)
                return _cache;

            var rows = await GetTripsFromAnyEndpointAsync(cancellationToken).ConfigureAwait(false);
            if (rows is null || rows.Count == 0)
            {
                _cache = Array.Empty<TripHistoryItem>();
                return _cache;
            }

            var list = new List<TripHistoryItem>();
            foreach (var r in rows)
            {
                var item = ToTrip(r);
                if (item is not null)
                    list.Add(item);
            }

            _cache = list;
            return _cache;
        }
        catch
        {
            _cache = Array.Empty<TripHistoryItem>();
            return _cache;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    public static async Task<IReadOnlyList<TripMonthGroup>> GetGroupedByMonthAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var all = await GetAllTripsAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
        return all
            .GroupBy(t => t.MonthGroupTitle)
            .OrderByDescending(g => g.Max(x => x.WhenLocal))
            .Select(g => new TripMonthGroup(g.Key, g.OrderByDescending(x => x.WhenLocal)))
            .ToList();
    }

    public static void InvalidateCache() => _cache = null;

    public static async Task<TripHistoryItem?> FindByIdAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var direct = await FindByIdFromAnyEndpointAsync(id, cancellationToken).ConfigureAwait(false);
        if (direct is not null)
            return direct;

        var all = await GetAllTripsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(t => t.Id == id);
    }

    static async Task<List<TripRowDto>?> GetTripsFromAnyEndpointAsync(CancellationToken cancellationToken)
    {
        var routes = new[] { "/api/rides/history", "/api/rides", "/api/trips", "/api/trip" };

        foreach (var route in routes)
        {
            try
            {
                using var request = await CreateGetRequestAsync(route, cancellationToken).ConfigureAwait(false);
                using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    continue;

                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var rows = ParseTripRows(payload);
                if (rows.Count > 0)
                    return rows;
            }
            catch
            {
                // thử endpoint tiếp theo
            }
        }

        return null;
    }

    static async Task<TripHistoryItem?> FindByIdFromAnyEndpointAsync(string id, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(id);
        var latitude = await Location.GetCurrentLatitudeAsync(cancellationToken).ConfigureAwait(false);
        var latQuery = (latitude ?? 10.8).ToString(CultureInfo.InvariantCulture);

        var routes = new[]
        {
            $"/api/trips/{encodedId}?latitude={latQuery}",
            $"/api/trip/{encodedId}?latitude={latQuery}",
            $"/api/trips/{encodedId}",
            $"/api/trip/{encodedId}",
            $"/api/rides/{encodedId}"
        };

        foreach (var route in routes)
        {
            try
            {
                using var request = await CreateGetRequestAsync(route, cancellationToken).ConfigureAwait(false);
                using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    continue;

                var row = await response.Content.ReadFromJsonAsync<TripRowDto>(JsonOptions, cancellationToken).ConfigureAwait(false);
                if (row is null)
                    continue;

                return ToTrip(row);
            }
            catch
            {
                // thử endpoint tiếp theo
            }
        }

        return null;
    }

    static List<TripRowDto> ParseTripRows(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new List<TripRowDto>();

        try
        {
            var rows = JsonSerializer.Deserialize<List<TripRowDto>>(payload, JsonOptions);
            if (rows is { Count: > 0 })
                return rows;
        }
        catch
        {
            // thử format wrapper
        }

        try
        {
            var wrapped = JsonSerializer.Deserialize<TripListResponseDto>(payload, JsonOptions);
            if (wrapped?.Trips is { Count: > 0 })
                return wrapped.Trips;
        }
        catch
        {
            // ignore
        }

        return new List<TripRowDto>();
    }

    static TripHistoryItem? ToTrip(TripRowDto r)
    {
        DateTime when;
        if (r.CreatedAt.HasValue)
        {
            when = r.CreatedAt.Value;
        }
        else if (!string.IsNullOrWhiteSpace(r.WhenLocal)
                 && DateTime.TryParse(r.WhenLocal, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            when = parsed;
        }
        else
        {
            return null;
        }

        var id = !string.IsNullOrWhiteSpace(r.Id)
            ? r.Id.Trim()
            : r.TripId?.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var from = !string.IsNullOrWhiteSpace(r.From)
            ? r.From.Trim()
            : r.StartLat.HasValue && r.StartLng.HasValue
                ? $"{r.StartLat.Value:F5}, {r.StartLng.Value:F5}"
                : "Điểm đón";

        var to = !string.IsNullOrWhiteSpace(r.To)
            ? r.To.Trim()
            : r.EndLat.HasValue && r.EndLng.HasValue
                ? $"{r.EndLat.Value:F5}, {r.EndLng.Value:F5}"
                : "Điểm đến";

        var driverName = !string.IsNullOrWhiteSpace(r.DriverName)
            ? r.DriverName.Trim()
            : r.DriverId.HasValue ? $"Tài xế #{r.DriverId.Value}" : "Chưa gán tài xế";

        var vehicleInfo = !string.IsNullOrWhiteSpace(r.VehicleInfo)
            ? r.VehicleInfo.Trim()
            : r.PaymentAmount.HasValue
                ? $"Đã thanh toán {r.PaymentAmount.Value:N0}đ"
                : "Chưa có thông tin xe";

        var status = !string.IsNullOrWhiteSpace(r.Status) ? r.Status.Trim() : "Unknown";

        var priceVnd = r.PriceVnd > 0 ? r.PriceVnd : (r.Price ?? 0);

        return new TripHistoryItem
        {
            Id = id,
            From = from,
            To = to,
            WhenLocal = when,
            DriverName = driverName,
            VehicleInfo = vehicleInfo,
            PriceVnd = priceVnd,
            Status = status,
        };
    }

    static async Task<HttpRequestMessage> CreateGetRequestAsync(string route, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, WebApiServerConfig.BuildUrl(route));

        var token = Session.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var latitude = await Location.GetCurrentLatitudeAsync(cancellationToken).ConfigureAwait(false);
        if (latitude.HasValue)
            request.Headers.TryAddWithoutValidation("X-User-Latitude", latitude.Value.ToString(CultureInfo.InvariantCulture));

        return request;
    }
}
