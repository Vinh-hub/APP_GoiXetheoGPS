using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

/// <summary>
/// Service lấy thống kê DB phân tán qua Web API.
/// </summary>
public static class DistributedDatabaseService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly AuthSessionService Session = new();
    private static readonly UserLocationService Location = new();

    public sealed record DatabaseStats(
        string DatabaseName,
        int RecordCount,
        DateTime LastUpdated);

    /// ================================
    /// LẤY CẢ 2 DB
    /// ================================
    public static async Task<List<DatabaseStats>> GetDatabaseStatsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetStatsFromAnyEndpointAsync(
            cancellationToken,
            "/api/distributed-db/stats",
            "/api/database/stats",
            "/api/distributeddb/stats");
    }

    /// ================================
    /// DB1 RIÊNG
    /// ================================
    public static async Task<DatabaseStats> GetPrimaryDatabaseStatsAsync(CancellationToken cancellationToken = default)
    {
        return await GetSingleFromAnyEndpointAsync(
            cancellationToken,
            fallbackName: "DB1 (Primary)",
            "/api/distributed-db/stats/primary",
            "/api/database/stats/primary",
            "/api/distributeddb/stats/primary");
    }

    /// ================================
    /// DB2 RIÊNG
    /// ================================
    public static async Task<DatabaseStats> GetSecondaryDatabaseStatsAsync(CancellationToken cancellationToken = default)
    {
        return await GetSingleFromAnyEndpointAsync(
            cancellationToken,
            fallbackName: "DB2 (Replica)",
            "/api/distributed-db/stats/secondary",
            "/api/distributed-db/stats/replica",
            "/api/database/stats/secondary");
    }

    /// ================================
    /// WEB API HELPERS
    /// ================================
    private static async Task<List<DatabaseStats>> GetStatsFromAnyEndpointAsync(
        CancellationToken cancellationToken,
        params string[] routes)
    {
        Exception? lastError = null;

        foreach (var route in routes)
        {
            try
            {
                var url = WebApiServerConfig.BuildUrl(route);
                using var request = await CreateGetRequestAsync(url, cancellationToken);
                using var response = await Http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue;

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                var stats = ReadStatsList(payload);
                if (stats.Count > 0)
                    return stats;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Database API error: {lastError.Message}");
        }

        return new List<DatabaseStats>
        {
            new("DB1 (Primary)", 0, DateTime.Now),
            new("DB2 (Replica)", 0, DateTime.Now)
        };
    }

    private static async Task<DatabaseStats> GetSingleFromAnyEndpointAsync(
        CancellationToken cancellationToken,
        string fallbackName,
        params string[] routes)
    {
        Exception? lastError = null;

        foreach (var route in routes)
        {
            try
            {
                var url = WebApiServerConfig.BuildUrl(route);
                using var request = await CreateGetRequestAsync(url, cancellationToken);
                using var response = await Http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue;

                var dto = await response.Content.ReadFromJsonAsync<DatabaseStats>(JsonOptions, cancellationToken);
                if (dto is not null)
                    return Normalize(dto, fallbackName);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Database API error: {lastError.Message}");
        }

        return new DatabaseStats(fallbackName, 0, DateTime.Now);
    }

    private static List<DatabaseStats> ReadStatsList(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new List<DatabaseStats>();

        try
        {
            var direct = JsonSerializer.Deserialize<List<DatabaseStats>>(payload, JsonOptions);
            if (direct is { Count: > 0 })
                return direct.Select(Normalize).ToList();
        }
        catch
        {
            // try wrapper format below
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("stats", out var wrapped)
            && wrapped.ValueKind == JsonValueKind.Array)
        {
            var list = wrapped.Deserialize<List<DatabaseStats>>(JsonOptions);
            if (list is { Count: > 0 })
                return list.Select(Normalize).ToList();
        }

        return new List<DatabaseStats>();
    }

    private static DatabaseStats Normalize(DatabaseStats stat)
    {
        var name = string.IsNullOrWhiteSpace(stat.DatabaseName)
            ? "DB"
            : stat.DatabaseName.Trim();

        return stat with
        {
            DatabaseName = name,
            LastUpdated = stat.LastUpdated == default ? DateTime.Now : stat.LastUpdated
        };
    }

    private static DatabaseStats Normalize(DatabaseStats stat, string fallbackName)
    {
        var normalized = Normalize(stat);
        return string.IsNullOrWhiteSpace(stat.DatabaseName)
            ? normalized with { DatabaseName = fallbackName }
            : normalized;
    }

    private static async Task<HttpRequestMessage> CreateGetRequestAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var token = Session.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var latitude = await Location.GetCurrentLatitudeAsync(cancellationToken);
        if (latitude.HasValue)
            request.Headers.TryAddWithoutValidation("X-User-Latitude", latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return request;
    }

    /// ================================
    /// SAVE CACHE
    /// ================================
    public static async Task SaveStatsAsync(List<DatabaseStats> stats)
    {
        try
        {
            var json = JsonSerializer.Serialize(stats);
            var path = Path.Combine(
                FileSystem.Current.AppDataDirectory,
                "db_stats.json");

            await File.WriteAllTextAsync(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error saving stats: {ex.Message}");
        }
    }

    /// ================================
    /// LOAD CACHE
    /// ================================
    public static async Task<List<DatabaseStats>?> LoadStatsAsync()
    {
        try
        {
            var path = Path.Combine(
                FileSystem.Current.AppDataDirectory,
                "db_stats.json");

            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);

            return JsonSerializer.Deserialize<List<DatabaseStats>>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error loading stats: {ex.Message}");

            return null;
        }
    }
}