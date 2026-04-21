using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

public sealed class AuthSessionService
{
    const string AccessTokenKey = "auth_access_token";

    public string? AccessToken
    {
        get => Preferences.Default.Get<string?>(AccessTokenKey, null);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                Preferences.Default.Remove(AccessTokenKey);
            else
                Preferences.Default.Set(AccessTokenKey, value);
        }
    }

    public void Clear() => Preferences.Default.Remove(AccessTokenKey);
}
