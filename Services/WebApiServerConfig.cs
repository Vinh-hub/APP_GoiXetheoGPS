using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

public static class WebApiServerConfig
{
    const string ApiBaseUrlPreferenceKey = "web_api_base_url";

    public static string BaseUrl
    {
        get
        {
            var savedValue = Preferences.Default.Get<string?>(ApiBaseUrlPreferenceKey, null);
            return NormalizeBaseUrl(savedValue);
        }
        set => Preferences.Default.Set(ApiBaseUrlPreferenceKey, NormalizeBaseUrl(value));
    }

    public static string BuildUrl(string route)
    {
        var normalizedRoute = route.StartsWith('/') ? route : $"/{route}";
        return $"{BaseUrl}{normalizedRoute}";
    }

    static string GetDefaultBaseUrl()
    {
        return DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5136"
            : "http://localhost:5136";
    }

    static string NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return GetDefaultBaseUrl();

        var trimmed = value.Trim();
        var normalized = trimmed.EndsWith('/') ? trimmed.TrimEnd('/') : trimmed;

        if (DeviceInfo.Current.Platform != DevicePlatform.Android)
            return normalized;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return normalized;

        if (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return normalized;

        var builder = new UriBuilder(uri)
        {
            Host = "10.0.2.2"
        };

        return builder.Uri.GetLeftPart(UriPartial.Authority);
    }
}
