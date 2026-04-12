using MySql.Data.MySqlClient;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace APP_GoiXetheoGPS.Services;

/// <summary>
/// Service kết nối THẬT tới 2 database MySQL phân tán
/// </summary>
public static class DistributedDatabaseService
{
    /// ================================
    /// CONFIG DATABASE CONNECTION
    /// ================================
    private const string DB1_CONNECTION =
        "Server=127.0.0.1;Port=3306;Database=northdb;Uid=root;Pwd=123456;";

    private const string DB2_CONNECTION =
        "Server=127.0.0.1;Port=3306;Database=southdb;Uid=root;Pwd=123456;";

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
        var stats = new List<DatabaseStats>();

        var db1Count = await GetRealRecordCountAsync(
            DB1_CONNECTION,
            "users",
            cancellationToken);

        stats.Add(new DatabaseStats(
            "DB1 (Primary)",
            db1Count,
            DateTime.Now));

        var db2Count = await GetRealRecordCountAsync(
            DB2_CONNECTION,
            "users",
            cancellationToken);

        stats.Add(new DatabaseStats(
            "DB2 (Replica)",
            db2Count,
            DateTime.Now));

        return stats;
    }

    /// ================================
    /// DB1 RIÊNG
    /// ================================
    public static async Task<DatabaseStats> GetPrimaryDatabaseStatsAsync()
    {
        var count = await GetRealRecordCountAsync(DB1_CONNECTION, "users");

        return new DatabaseStats(
            "DB1 (Primary)",
            count,
            DateTime.Now);
    }

    /// ================================
    /// DB2 RIÊNG
    /// ================================
    public static async Task<DatabaseStats> GetSecondaryDatabaseStatsAsync()
    {
        var count = await GetRealRecordCountAsync(DB2_CONNECTION, "users");

        return new DatabaseStats(
            "DB2 (Replica)",
            count,
            DateTime.Now);
    }

    /// ================================
    /// QUERY COUNT THẬT
    /// ================================
    private static async Task<int> GetRealRecordCountAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            string query = $"SELECT COUNT(*) FROM {tableName}";
            using var cmd = new MySqlCommand(query, conn);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Database error: {ex.Message}");

            return 0;
        }
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