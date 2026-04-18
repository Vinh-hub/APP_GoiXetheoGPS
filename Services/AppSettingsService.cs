using System.Globalization;
using APP_GoiXetheoGPS.Models;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

public sealed class AppSettingsService
{
    const string ApiBaseUrlKey = "demo_api_base_url";
    const string JwtTokenKey = "demo_api_jwt_token";
    const string RegionKey = "demo_region_key";

    private static readonly List<RegionOption> RegionOptions =
        new List<RegionOption>
        {
            new()
            {
                Key = "hanoi",
                CityName = "Hà Nội",
                RoutingBucket = "Bắc",
                SampleLatitude = 21.0285,
                SampleLongitude = 105.8542
            },
            new()
            {
                Key = "haiphong",
                CityName = "Hải Phòng",
                RoutingBucket = "Bắc",
                SampleLatitude = 20.8449,
                SampleLongitude = 106.6881
            },
            new()
            {
                Key = "danang",
                CityName = "Đà Nẵng",
                RoutingBucket = "Bắc",
                SampleLatitude = 16.0544,
                SampleLongitude = 108.2022
            },
            new()
            {
                Key = "hcm",
                CityName = "TP.HCM",
                RoutingBucket = "Nam",
                SampleLatitude = 10.8231,
                SampleLongitude = 106.6297
            },
            new()
            {
                Key = "cantho",
                CityName = "Cần Thơ",
                RoutingBucket = "Nam",
                SampleLatitude = 10.0452,
                SampleLongitude = 105.7469
            }
        };

    public List<RegionOption> GetRegionOptions() => RegionOptions;

    public RegionOption GetSelectedRegion()
    {
        var savedKey = Preferences.Default.Get(RegionKey, RegionOptions[0].Key);
        return RegionOptions.FirstOrDefault(x => x.Key == savedKey) ?? RegionOptions[0];
    }

    public void SaveSelectedRegion(string? key)
    {
        var region = RegionOptions.FirstOrDefault(x => x.Key == key) ?? RegionOptions[0];
        Preferences.Default.Set(RegionKey, region.Key);
    }

    public string GetApiBaseUrl()
        => Preferences.Default.Get(ApiBaseUrlKey, string.Empty).Trim();

    public void SaveApiBaseUrl(string? baseUrl)
    {
        var normalized = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        Preferences.Default.Set(ApiBaseUrlKey, normalized);
    }

    public string GetJwtToken()
        => Preferences.Default.Get(JwtTokenKey, string.Empty).Trim();

    public void SaveJwtToken(string? token)
    {
        Preferences.Default.Set(JwtTokenKey, (token ?? string.Empty).Trim());
    }

    public string GetUserLatitudeHeaderValue()
        => GetSelectedRegion().SampleLatitude.ToString(CultureInfo.InvariantCulture);

    public bool HasApiBaseUrlConfigured()
        => !string.IsNullOrWhiteSpace(GetApiBaseUrl());

    public bool HasJwtConfigured()
        => !string.IsNullOrWhiteSpace(GetJwtToken());

    public string BuildConfigurationSummary()
    {
        var region = GetSelectedRegion();
        var baseUrl = GetApiBaseUrl();
        var token = GetJwtToken();

        var apiText = string.IsNullOrWhiteSpace(baseUrl)
            ? "Chưa cấu hình Base URL"
            : $"API: {baseUrl}";
        var tokenText = string.IsNullOrWhiteSpace(token)
            ? "JWT: chưa có"
            : $"JWT: {MaskToken(token)}";

        return $"{apiText} • {tokenText} • Vùng demo: {region.DisplayName}";
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 10)
            return token;

        return $"{token[..6]}...{token[^4..]}";
    }
}
