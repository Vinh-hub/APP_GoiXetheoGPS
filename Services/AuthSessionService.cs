using Microsoft.Maui.Storage;
using System.Text;
using System.Text.Json;

namespace APP_GoiXetheoGPS.Services;

public sealed class AuthSessionService
{
    const string AccessTokenKey = "auth_access_token";
    const string RefreshTokenKey = "auth_refresh_token";
    const string UserIdKey = "auth_user_id";
    const string RoleKey = "auth_role";
    const string EmailKey = "auth_email";
    const string NameKey = "auth_name";
    const string RegionIdKey = "auth_region_id";

    string? _accessToken;
    string? _refreshToken;
    bool _restored;

    public event EventHandler? LoginStateChanged;

    public string? AccessToken
    {
        get => _accessToken ?? Preferences.Default.Get<string?>(AccessTokenKey, null);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                Clear();
            else
            {
                _accessToken = value;
                // Persist token in Preferences for reliable restore (esp. Windows).
                // SecureStorage is best-effort; failures shouldn't drop the token.
                Preferences.Default.Set(AccessTokenKey, value);
                _ = TrySetSecureAsync(AccessTokenKey, value);
            }
        }
    }

    public string? RefreshToken
    {
        get => _refreshToken ?? Preferences.Default.Get<string?>(RefreshTokenKey, null);
        private set
        {
            _refreshToken = value;
            if (string.IsNullOrWhiteSpace(value))
            {
                Preferences.Default.Remove(RefreshTokenKey);
                SecureStorage.Default.Remove(RefreshTokenKey);
            }
            else
            {
                Preferences.Default.Set(RefreshTokenKey, value);
                _ = TrySetSecureAsync(RefreshTokenKey, value);
            }
        }
    }

    public int UserId
    {
        get => Preferences.Default.Get(UserIdKey, 0);
        set => Preferences.Default.Set(UserIdKey, value);
    }

    public string Role
    {
        get => Preferences.Default.Get(RoleKey, string.Empty);
        set => Preferences.Default.Set(RoleKey, value ?? string.Empty);
    }

    public string Email
    {
        get => Preferences.Default.Get(EmailKey, string.Empty);
        set => Preferences.Default.Set(EmailKey, value ?? string.Empty);
    }

    public string Name
    {
        get => Preferences.Default.Get(NameKey, string.Empty);
        set => Preferences.Default.Set(NameKey, value ?? string.Empty);
    }

    public int RegionId
    {
        get => Preferences.Default.Get(RegionIdKey, 0);
        set => Preferences.Default.Set(RegionIdKey, value);
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken) && !IsTokenExpired();

    private bool IsTokenExpiredSoon()
    {
        var expires = GetTokenExpiryUtc();
        if (!expires.HasValue)
            return false;

        // Token considered expired soon if less than 1 minute remaining
        return expires.Value <= DateTimeOffset.UtcNow.AddMinutes(1);
    }

    public async Task RestoreAsync()
    {
        if (_restored)
            return;

        try
        {
            _accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
            _refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        }
        catch
        {
            // SecureStorage can fail on some platforms; fall back to Preferences.
        }

        var legacyToken = Preferences.Default.Get<string?>(AccessTokenKey, null);
        if (string.IsNullOrWhiteSpace(_accessToken) && !string.IsNullOrWhiteSpace(legacyToken))
        {
            _accessToken = legacyToken;
            _ = TrySetSecureAsync(AccessTokenKey, legacyToken);
        }

        var prefRefresh = Preferences.Default.Get<string?>(RefreshTokenKey, null);
        if (string.IsNullOrWhiteSpace(_refreshToken) && !string.IsNullOrWhiteSpace(prefRefresh))
            _refreshToken = prefRefresh;

        _restored = true;
        if (IsTokenExpired())
            Clear();
    }

    public void RefreshLoginFromStorage()
    {
        _ = RestoreAsync();
    }

    public async Task SaveLoginAsync(AuthApiService.AuthResponse response)
    {
        if (response is null || string.IsNullOrWhiteSpace(response.Token))
            return;

        _accessToken = response.Token;
        Preferences.Default.Set(AccessTokenKey, response.Token);
        await TrySetSecureAsync(AccessTokenKey, response.Token);

        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            _refreshToken = response.RefreshToken;
            Preferences.Default.Set(RefreshTokenKey, response.RefreshToken);
            await TrySetSecureAsync(RefreshTokenKey, response.RefreshToken);
        }

        UserId = response.UserId;
        Role = response.Role ?? string.Empty;
        Email = response.Email ?? string.Empty;
        Name = response.Name ?? string.Empty;
        RegionId = response.RegionId;

        // Fire event when login changes
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public DateTimeOffset? GetTokenExpiryUtc()
    {
        var payload = ReadJwtPayload(AccessToken);
        if (payload is null)
            return null;

        if (!payload.RootElement.TryGetProperty("exp", out var expElement))
            return null;

        if (expElement.ValueKind == JsonValueKind.Number && expElement.TryGetInt64(out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);

        if (expElement.ValueKind == JsonValueKind.String && long.TryParse(expElement.GetString(), out unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);

        return null;
    }

    public bool IsTokenExpired()
    {
        var expires = GetTokenExpiryUtc();
        return expires.HasValue && expires.Value <= DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        _accessToken = null;
        _refreshToken = null;
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        Preferences.Default.Remove(AccessTokenKey);
        Preferences.Default.Remove(RefreshTokenKey);
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(RoleKey);
        Preferences.Default.Remove(EmailKey);
        Preferences.Default.Remove(NameKey);
        Preferences.Default.Remove(RegionIdKey);

        // Fire event when logout happens
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    static JsonDocument? ReadJwtPayload(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(payload);
            return JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch
        {
            return null;
        }
    }

    static async Task TrySetSecureAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            // Best-effort: Preferences already contains the value.
        }
    }
}
