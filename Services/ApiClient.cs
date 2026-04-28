using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace APP_GoiXetheoGPS.Services;

public sealed class ApiClient
{
    readonly HttpClient _http;
    readonly AuthSessionService _session;
    readonly UserLocationService _location;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(AuthSessionService session, UserLocationService location)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _session = session;
        _location = location;
    }

    public Task<T?> GetAsync<T>(string route, bool requiresAuth = true, CancellationToken cancellationToken = default)
        => SendAsync<T>(HttpMethod.Get, route, null, requiresAuth, cancellationToken);

    public Task<T?> PostAsync<TRequest, T>(string route, TRequest body, bool requiresAuth = true, CancellationToken cancellationToken = default)
        => SendAsync<T>(HttpMethod.Post, route, body, requiresAuth, cancellationToken);

    public Task PostAsync<TRequest>(string route, TRequest body, bool requiresAuth = true, CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Post, route, body, requiresAuth, cancellationToken);

    async Task<T?> SendAsync<T>(HttpMethod method, string route, object? body, bool requiresAuth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, WebApiServerConfig.BuildUrl(route));

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        await AddHeadersAsync(request, requiresAuth, cancellationToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, requiresAuth);

        if (typeof(T) == typeof(object) || response.Content is null)
            return default;

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return default;

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    async Task AddHeadersAsync(HttpRequestMessage request, bool requiresAuth, CancellationToken cancellationToken)
    {
        if (requiresAuth)
        {
            var token = _session.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiRequestException(HttpStatusCode.Unauthorized, "Thiếu token đăng nhập.");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var latitude = await _location.GetCurrentLatitudeAsync(cancellationToken);
        if (latitude.HasValue)
        {
            request.Headers.Remove("X-User-Latitude");
            request.Headers.TryAddWithoutValidation("X-User-Latitude", latitude.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken, bool requiresAuth)
    {
        if (response.IsSuccessStatusCode)
            return;

        var apiMessage = await TryReadErrorMessageAsync(response, cancellationToken);
        if (requiresAuth && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _session.Clear();
            throw new ApiRequestException(HttpStatusCode.Unauthorized,
                string.IsNullOrWhiteSpace(apiMessage)
                    ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                    : apiMessage);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new ApiReadOnlyException(apiMessage);

        throw new ApiRequestException(response.StatusCode,
            string.IsNullOrWhiteSpace(apiMessage)
                ? $"API lỗi {(int)response.StatusCode}."
                : apiMessage);
    }

    static async Task<string?> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
            return null;

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String)
                return messageEl.GetString();

            if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.String)
                return errorEl.GetString();
        }
        catch
        {
            // ignore
        }

        return payload;
    }
}
