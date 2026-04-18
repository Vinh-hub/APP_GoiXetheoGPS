using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using APP_GoiXetheoGPS.Models;

namespace APP_GoiXetheoGPS.Services;

public sealed class RideApiService
{
    private readonly AppSettingsService _settingsService;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RideApiService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<IReadOnlyList<NearbyDriverItem>> GetNearbyDriversAsync(
        double latitude,
        double longitude,
        double radiusKm = 10,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var endpoint =
            $"{BuildBaseUrl()}/api/drivers/nearby?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&radiusKm={radiusKm.ToString(CultureInfo.InvariantCulture)}" +
            $"&limit={limit}";

        using var request = BuildRequest(HttpMethod.Get, endpoint, includeJwt: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var items = await response.Content.ReadFromJsonAsync<List<NearbyDriverItem>>(JsonOptions, cancellationToken);
        return items ?? new List<NearbyDriverItem>();
    }

    public async Task<CreateRideResponse> BookRideAsync(
        CreateRideRequest requestModel,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"{BuildBaseUrl()}/api/rides";
        using var request = BuildRequest(HttpMethod.Post, endpoint, includeJwt: true);
        request.Content = JsonContent.Create(requestModel);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<CreateRideApiResponse>(JsonOptions, cancellationToken);
        return new CreateRideResponse
        {
            TripId = payload?.TripId ?? 0,
            Message = payload?.Message ?? "Đặt chuyến thành công"
        };
    }

    public async Task<IReadOnlyList<RideHistoryDto>> GetRideHistoryAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = $"{BuildBaseUrl()}/api/rides/history";
        using var request = BuildRequest(HttpMethod.Get, endpoint, includeJwt: true);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var items = await response.Content.ReadFromJsonAsync<List<RideHistoryDto>>(JsonOptions, cancellationToken);
        return items ?? new List<RideHistoryDto>();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, bool includeJwt)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-User-Latitude", _settingsService.GetUserLatitudeHeaderValue());

        if (includeJwt)
        {
            var token = _settingsService.GetJwtToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new RideApiException("Chưa cấu hình JWT để gọi API.", HttpStatusCode.Unauthorized);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private string BuildBaseUrl()
    {
        var baseUrl = _settingsService.GetApiBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new RideApiException("Chưa cấu hình Base URL của backend.", HttpStatusCode.BadRequest);

        return baseUrl;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ExtractApiErrorMessage(body);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            if (message.Contains("chỉ đọc", StringComparison.OrdinalIgnoreCase))
            {
                throw new RideApiException(
                    "Hệ thống đang ở chế độ chỉ đọc, không thể đặt chuyến mới.",
                    response.StatusCode,
                    isReadOnly: true);
            }

            throw new RideApiException(
                string.IsNullOrWhiteSpace(message) ? "Backend đang tạm thời không sẵn sàng." : message,
                response.StatusCode);
        }

        throw new RideApiException(
            string.IsNullOrWhiteSpace(message) ? $"Gọi API thất bại ({(int)response.StatusCode})." : message,
            response.StatusCode);
    }

    private static string ExtractApiErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var propertyName in new[] { "message", "error", "detail", "title" })
            {
                if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                    return property.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // Fall back to raw response text.
        }

        return body.Trim();
    }

    private sealed class CreateRideApiResponse
    {
        public int TripId { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}

public sealed class RideApiException : Exception
{
    public RideApiException(string message, HttpStatusCode statusCode, bool isReadOnly = false)
        : base(message)
    {
        StatusCode = statusCode;
        IsReadOnly = isReadOnly;
    }

    public HttpStatusCode StatusCode { get; }

    public bool IsReadOnly { get; }
}
