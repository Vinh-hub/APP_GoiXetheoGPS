using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using APP_GoiXetheoGPS.Models;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

/// <summary>
/// Đọc danh sách chuyến từ <c>Data/trips_for_app.json</c> (đóng gói MauiAsset tên <c>trips_for_app.json</c>).
/// Nội dung JSON bám seed <c>Data/APPGPS.sql</c> (Trips/Drivers/Vehicles); APPGPS.sql là MySQL, app dùng bản chiếu JSON.
/// </summary>
public static class TripDataStore
{
    const string PackagedFileName = "trips_for_app.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    static readonly SemaphoreSlim LoadGate = new(1, 1);
    static IReadOnlyList<TripHistoryItem>? _cache;

    sealed class TripFileDto
    {
        public List<TripRowDto>? Trips { get; set; }
    }

    sealed class TripRowDto
    {
        public string? Id { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? WhenLocal { get; set; }
        public string? DriverName { get; set; }
        public string? VehicleInfo { get; set; }
        public decimal PriceVnd { get; set; }
        public string? Status { get; set; }

        [JsonPropertyName("sqlNote")]
        public string? SqlNote { get; set; }
    }

    public static async Task<IReadOnlyList<TripHistoryItem>> GetAllTripsAsync(CancellationToken cancellationToken = default)
    {
        await LoadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache is not null)
                return _cache;

            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(PackagedFileName).ConfigureAwait(false);
            var dto = await JsonSerializer.DeserializeAsync<TripFileDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            var rows = dto?.Trips;
            if (rows is null || rows.Count == 0)
            {
                _cache = Array.Empty<TripHistoryItem>();
                return _cache;
            }

            var list = new List<TripHistoryItem>();
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.Id)
                    || string.IsNullOrWhiteSpace(r.From)
                    || string.IsNullOrWhiteSpace(r.To)
                    || string.IsNullOrWhiteSpace(r.WhenLocal)
                    || string.IsNullOrWhiteSpace(r.DriverName)
                    || string.IsNullOrWhiteSpace(r.VehicleInfo)
                    || string.IsNullOrWhiteSpace(r.Status))
                    continue;

                if (!DateTime.TryParse(r.WhenLocal, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var when))
                    continue;

                list.Add(new TripHistoryItem
                {
                    Id = r.Id.Trim(),
                    From = r.From.Trim(),
                    To = r.To.Trim(),
                    WhenLocal = when,
                    DriverName = r.DriverName.Trim(),
                    VehicleInfo = r.VehicleInfo.Trim(),
                    PriceVnd = r.PriceVnd,
                    Status = r.Status.Trim(),
                });
            }

            _cache = list;
            return _cache;
        }
        catch
        {
            _cache = Array.Empty<TripHistoryItem>();
            return _cache;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    public static async Task<IReadOnlyList<TripMonthGroup>> GetGroupedByMonthAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllTripsAsync(cancellationToken).ConfigureAwait(false);
        return all
            .GroupBy(t => t.MonthGroupTitle)
            .OrderByDescending(g => g.Max(x => x.WhenLocal))
            .Select(g => new TripMonthGroup(g.Key, g.OrderByDescending(x => x.WhenLocal)))
            .ToList();
    }

    public static async Task<TripHistoryItem?> FindByIdAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var all = await GetAllTripsAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(t => t.Id == id);
    }
}
