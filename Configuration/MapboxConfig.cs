namespace APP_GoiXetheoGPS.Configuration;

/// <summary>
/// Public token Mapbox (pk.*) — nên giới hạn theo URL/bundle trong Mapbox Account.
/// Có thể thay bằng biến môi trường / user secrets cho bản phát hành.
/// </summary>
public static class MapboxConfig
{
    private const string TokenPreferenceKey = "MAPBOX_ACCESS_TOKEN";

    /// <summary>
    /// Mapbox Public Access Token. Provide via environment variable: MAPBOX_ACCESS_TOKEN.
    /// </summary>
    public static string AccessToken => GetAccessToken();

    public static bool HasAccessTokenConfigured
    {
        get
        {
            return TryGetAccessToken(out _);
        }
    }

    public static bool TryGetAccessToken(out string token)
    {
        var env = Environment.GetEnvironmentVariable("MAPBOX_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
        {
            token = env.Trim();
            return true;
        }

        var pref = Microsoft.Maui.Storage.Preferences.Get(TokenPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(pref))
        {
            token = pref.Trim();
            return true;
        }

        token = string.Empty;
        return false;
    }

    public static void SaveAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        Microsoft.Maui.Storage.Preferences.Set(TokenPreferenceKey, token.Trim());
    }

    private static string GetAccessToken()
    {
        if (TryGetAccessToken(out var token))
            return token;

        // Intentionally NOT a real token. Keeps the app buildable while avoiding secrets in git history.
        return "YOUR_MAPBOX_PUBLIC_TOKEN";
    }
}
