using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

public static class WebApiServerConfig
{
    const string ApiBaseUrlPreferenceKey = "web_api_base_url";

    public static string BaseUrl
    {
        get => Preferences.Default.Get(ApiBaseUrlPreferenceKey, GetDefaultBaseUrl());
        set => Preferences.Default.Set(ApiBaseUrlPreferenceKey, NormalizeBaseUrl(value));
    }

    public static string BuildUrl(string route)
    {
        var normalizedRoute = route.StartsWith('/') ? route : $"/{route}";
        return $"{BaseUrl}{normalizedRoute}";
    }

    static string GetDefaultBaseUrl()
    {
        var host = DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "10.0.2.2"
            : "127.0.0.1";

        return $"http://{host}:5136";
    }

    static string NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return GetDefaultBaseUrl();

        var trimmed = value.Trim();
        return trimmed.EndsWith('/') ? trimmed.TrimEnd('/') : trimmed;
    }
}
