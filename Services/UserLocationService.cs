using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

public sealed class UserLocationService
{
    const string PreferredRegionLatitudeKey = "preferred_region_latitude";
    const string PreferredRegionNameKey = "preferred_region_name";

    readonly SemaphoreSlim _gate = new(1, 1);
    double? _cachedLatitude;
    DateTime _cachedAt;

    public async Task<double?> GetCurrentLatitudeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var preferredLatitude = GetPreferredLatitude();
            if (preferredLatitude.HasValue)
                return preferredLatitude.Value;

            if (_cachedLatitude.HasValue && DateTime.UtcNow - _cachedAt < TimeSpan.FromMinutes(2))
                return _cachedLatitude.Value;

            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (permission != PermissionStatus.Granted)
                return null;

            var lastKnown = await Geolocation.GetLastKnownLocationAsync();
            if (lastKnown is not null && IsValidLatitude(lastKnown.Latitude))
            {
                Cache(lastKnown.Latitude);
                return lastKnown.Latitude;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
            var current = await Geolocation.GetLocationAsync(request, cancellationToken);
            if (current is not null && IsValidLatitude(current.Latitude))
            {
                Cache(current.Latitude);
                return current.Latitude;
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    static bool IsValidLatitude(double latitude)
        => latitude >= -90 && latitude <= 90 && !double.IsNaN(latitude) && !double.IsInfinity(latitude);

    void Cache(double latitude)
    {
        _cachedLatitude = latitude;
        _cachedAt = DateTime.UtcNow;
    }

    public void SetPreferredRegion(string regionName, double latitude)
    {
        if (!IsValidLatitude(latitude))
            return;

        Preferences.Default.Set(PreferredRegionLatitudeKey, latitude);
        Preferences.Default.Set(PreferredRegionNameKey, regionName ?? string.Empty);
        Cache(latitude);
    }

    public void ClearPreferredRegion()
    {
        Preferences.Default.Remove(PreferredRegionLatitudeKey);
        Preferences.Default.Remove(PreferredRegionNameKey);
    }

    public double? GetPreferredLatitude()
    {
        var hasPreferred = Preferences.Default.ContainsKey(PreferredRegionLatitudeKey);
        if (!hasPreferred)
            return null;

        var latitude = Preferences.Default.Get(PreferredRegionLatitudeKey, 0d);
        return IsValidLatitude(latitude) ? latitude : null;
    }

    public string? GetPreferredRegionName()
    {
        var name = Preferences.Default.Get<string?>(PreferredRegionNameKey, null);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
