using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Configuration;

/// <summary>
/// Public token Mapbox (pk.*) — nên giới hạn theo URL/bundle trong Mapbox Account.
/// Thứ tự: biến môi trường MAPBOX_ACCESS_TOKEN → MauiAsset <c>Resources/Raw/mapbox_token.txt</c> (gitignore) → Preferences.
/// Asset bundle tên <c>mapbox_token.txt</c> — khớp <c>LogicalName</c> của <c>MauiAsset Include="Resources\Raw\**"</c>.
/// Xem <c>Resources/Raw/mapbox_token.sample.txt</c> (trong repo) để biết cách tạo file token.
/// </summary>
public static class MapboxConfig
{
    private const string TokenPreferenceKey = "MAPBOX_ACCESS_TOKEN";
    private const string PackagedTokenFileName = "mapbox_token.txt";

    /// <summary>
    /// Mapbox Public Access Token.
    /// </summary>
    public static string AccessToken => GetAccessToken();

    public static bool HasAccessTokenConfigured
    {
        get
        {
            return TryGetAccessToken(out var t) && LooksLikeMapboxPublicToken(t);
        }
    }

    /// <summary>
    /// Token công khai Mapbox thường bắt đầu bằng pk. và khá dài; dùng để tránh gọi startMap với token rỗng/giả.
    /// </summary>
    public static bool LooksLikeMapboxPublicToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        var t = token.Trim();
        if (t.Equals("YOUR_MAPBOX_PUBLIC_TOKEN", StringComparison.OrdinalIgnoreCase))
            return false;
        return t.StartsWith("pk.", StringComparison.OrdinalIgnoreCase) && t.Length >= 50;
    }

    public static bool TryGetAccessToken(out string token)
    {
        var env = Environment.GetEnvironmentVariable("MAPBOX_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
        {
            token = env.Trim();
            return true;
        }

        var fromFile = ReadTokenFromTxtFile();
        if (!string.IsNullOrWhiteSpace(fromFile))
        {
            token = fromFile.Trim();
            return true;
        }

        var pref = Preferences.Get(TokenPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(pref))
        {
            token = pref.Trim();
            return true;
        }

        token = string.Empty;
        return false;
    }

    /// <summary>
    /// Đọc token từ MauiAsset <c>Resources/Raw/mapbox_token.txt</c> (tên gói: mapbox_token.txt).
    /// Dòng đầu không rỗng, không bắt đầu bằng #.
    /// </summary>
    private static string? ReadTokenFromTxtFile()
    {
        string? raw;
        try
        {
            using var stream = FileSystem.Current.OpenAppPackageFileAsync(PackagedTokenFileName).GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            raw = reader.ReadToEnd();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim().TrimStart('\ufeff');
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            return t;
        }

        return null;
    }

    public static void SaveAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        Preferences.Set(TokenPreferenceKey, token.Trim());
    }

    private static string GetAccessToken()
    {
        if (TryGetAccessToken(out var token))
            return token;

        // Intentionally NOT a real token. Keeps the app buildable while avoiding secrets in git history.
        return "YOUR_MAPBOX_PUBLIC_TOKEN";
    }
}
