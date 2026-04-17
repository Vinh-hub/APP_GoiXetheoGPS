using MySql.Data.MySqlClient;
using System.Text.Json;
using Microsoft.Maui.Devices;
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
    /// <summary>
    /// Android Emulator: 127.0.0.1 là chính emulator — MySQL trên PC phải dùng 10.0.2.2 (alias tới host).
    /// Điện thoại thật: đổi thành IP LAN của PC (ví dụ 192.168.1.5) trong my.ini / connection string tương ứng.
    /// </summary>
    private static string DbHost =>
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "10.0.2.2"
            : "127.0.0.1";

    // Trùng tên schema trong Data/APPGPS.sql và RideAPI appsettings.json (DistributedDb).
    private static string Db1Connection =>
        $"Server={DbHost};Port=3306;Database=NorthDB;Uid=root;Pwd=;";

    private static string Db2Connection =>
        $"Server={DbHost};Port=3306;Database=SouthDB;Uid=root;Pwd=;";

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
            Db1Connection,
            "Users",
            cancellationToken);

        stats.Add(new DatabaseStats(
            "DB1 (Primary)",
            db1Count,
            DateTime.Now));

        var db2Count = await GetRealRecordCountAsync(
            Db2Connection,
            "Users",
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
        var count = await GetRealRecordCountAsync(Db1Connection, "Users");

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
        var count = await GetRealRecordCountAsync(Db2Connection, "Users");

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