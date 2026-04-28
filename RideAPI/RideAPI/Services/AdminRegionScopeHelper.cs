using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace RideAPI.Services;

public static class AdminRegionScopeHelper
{
    public const string ScopeRegionCookie = "admin_scope_region_id";
    public const string ScopeLatitudeCookie = "admin_scope_latitude";
    public const string ScopeProvinceCookie = "admin_scope_province";

    public static int GetScopedRegionId(HttpRequest request, int fallbackRegionId)
    {
        var cookie = request.Cookies[ScopeRegionCookie];
        if (int.TryParse(cookie, out var regionId) && (regionId == 1 || regionId == 2))
            return regionId;

        return fallbackRegionId is 1 or 2 ? fallbackRegionId : 2;
    }

    public static string GetScopeLabel(int regionId)
        => regionId == 1 ? "Miền Bắc" : "Miền Nam";

    public static string GetScopeLatitudeText(HttpRequest request)
        => request.Cookies[ScopeLatitudeCookie] ?? string.Empty;

    public static string GetScopeProvince(HttpRequest request)
        => request.Cookies[ScopeProvinceCookie] ?? string.Empty;

    public static void SetScopeCookies(HttpResponse response, int regionId, double? latitude, string? province)
    {
        var options = new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        response.Cookies.Append(ScopeRegionCookie, regionId.ToString(CultureInfo.InvariantCulture), options);
        response.Cookies.Append(
            ScopeLatitudeCookie,
            latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            options);
        response.Cookies.Append(ScopeProvinceCookie, province?.Trim() ?? string.Empty, options);
    }
}
