using Microsoft.Maui.Storage;
using System.Text;
using System.Text.Json;

namespace APP_GoiXetheoGPS.Services;

public sealed class AuthSessionService
{
    const string AccessTokenKey = "auth_access_token";
    const string UserIdKey = "auth_user_id";
    const string RoleKey = "auth_role";
    const string EmailKey = "auth_email";
    const string NameKey = "auth_name";
    const string RegionIdKey = "auth_region_id";

    public string? AccessToken
    {
        get => Preferences.Default.Get<string?>(AccessTokenKey, null);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                Clear();
            else
                Preferences.Default.Set(AccessTokenKey, value);
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

    public void SaveLogin(AuthApiService.AuthResponse response)
    {
        if (response is null || string.IsNullOrWhiteSpace(response.Token))
            return;

        AccessToken = response.Token;
        UserId = response.UserId;
        Role = response.Role ?? string.Empty;
        Email = response.Email ?? string.Empty;
        Name = response.Name ?? string.Empty;
        RegionId = response.RegionId;
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
        Preferences.Default.Remove(AccessTokenKey);
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(RoleKey);
        Preferences.Default.Remove(EmailKey);
        Preferences.Default.Remove(NameKey);
        Preferences.Default.Remove(RegionIdKey);
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
}
