using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data.Common;
using System.Globalization;

namespace RideAPI.Services
{
    public class DatabaseService
    {
        private readonly string _northMasterConn;
        private readonly string _northReplicaConn;
        private readonly string _southMasterConn;
        private readonly string _southReplicaConn;

        public DatabaseService(IConfiguration config)
        {
            var host = config["DistributedDb:Host"] ?? "127.0.0.1";
            var port = config["DistributedDb:Port"] ?? "3306";
            var user = config["DistributedDb:User"] ?? "root";
            var password = config["DistributedDb:Password"] ?? "";
            var northDb = config["DistributedDb:NorthDatabase"] ?? "NorthDB";
            var southDb = config["DistributedDb:SouthDatabase"] ?? "SouthDB";

            string BuildConn(string database) =>
                $"Server={host};Port={port};Database={database};User={user};Password={password};";

            static string? EffectiveConn(string? value) =>
                string.IsNullOrWhiteSpace(value) ? null : value;

            var northPrimary = EffectiveConn(config["DistributedDb:North:Primary"])
                               ?? EffectiveConn(config.GetConnectionString("NorthMaster"))
                               ?? BuildConn(northDb);

            var northReplica = EffectiveConn(config["DistributedDb:North:Replica"])
                               ?? EffectiveConn(config.GetConnectionString("NorthReplica"))
                               ?? northPrimary;

            var southPrimary = EffectiveConn(config["DistributedDb:South:Primary"])
                               ?? EffectiveConn(config.GetConnectionString("SouthMaster"))
                               ?? BuildConn(southDb);

            var southReplica = EffectiveConn(config["DistributedDb:South:Replica"])
                               ?? EffectiveConn(config.GetConnectionString("SouthReplica"))
                               ?? southPrimary;

            _northMasterConn = NormalizeMySqlConnectionString(northPrimary);
            _northReplicaConn = NormalizeMySqlConnectionString(northReplica);
            _southMasterConn = NormalizeMySqlConnectionString(southPrimary);
            _southReplicaConn = NormalizeMySqlConnectionString(southReplica);
        }

        // Phương thức async dùng cho DbRetryService (hỗ trợ master/replica & failover)
        public async Task<NpgsqlConnection> GetConnectionAsync(string region, bool isWrite)
        {
            string masterConn, replicaConn;
            if (region == "NORTH")
            {
                masterConn = _northMasterConn;
                replicaConn = _northReplicaConn;
            }
            else if (region == "SOUTH")
            {
                masterConn = _southMasterConn;
                replicaConn = _southReplicaConn;
            }
            else
            {
                throw new ArgumentException("Invalid region", nameof(region));
            }

            if (isWrite)
            {
                var conn = new NpgsqlConnection(masterConn);
                if (await IsConnectionAlive(conn))
                    return conn;
                throw new Exception("MASTER_DOWN_CANNOT_WRITE");
            }
            else
            {
                var master = new NpgsqlConnection(masterConn);
                if (await IsConnectionAlive(master))
                    return master;
                var replica = new NpgsqlConnection(replicaConn);
                if (await IsConnectionAlive(replica))
                    return replica;
                throw new Exception("ALL_DB_NODES_DOWN");
            }
        }

        // Phương thức cũ giữ lại cho tương thích (đồng bộ, trả connection chưa mở)
        public NpgsqlConnection GetConnection(double latitude)
        {
            return latitude > 16
                ? new NpgsqlConnection(_northMasterConn)
                : new NpgsqlConnection(_southMasterConn);
        }

        private async Task<bool> IsConnectionAlive(NpgsqlConnection conn)
        {
            try
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync();
                await conn.CloseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeMySqlConnectionString(string connectionString)
        {
            try
            {
                var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };

                static string? GetValue(DbConnectionStringBuilder builder, params string[] keys)
                {
                    foreach (var key in keys)
                    {
                        if (builder.TryGetValue(key, out var value) && value is not null)
                            return Convert.ToString(value, CultureInfo.InvariantCulture);
                    }

                    return null;
                }

                var server = GetValue(raw, "Server", "Host", "Data Source") ?? "127.0.0.1";
                var database = GetValue(raw, "Database", "Initial Catalog") ?? string.Empty;
                var user = GetValue(raw, "User ID", "Uid", "User", "Username") ?? string.Empty;
                var password = GetValue(raw, "Password", "Pwd") ?? string.Empty;
                var portText = GetValue(raw, "Port");

                int port = 3306;
                if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsedPort))
                    port = parsedPort;

                var postgres = new NpgsqlConnectionStringBuilder
                {
                    Host = server,
                    Port = port,
                    Database = database,
                    Username = user,
                    Password = password
                };

                if (raw.TryGetValue("SSL Mode", out var sslMode) && sslMode is not null)
                {
                    var sslText = Convert.ToString(sslMode, CultureInfo.InvariantCulture);
                    if (string.Equals(sslText, "Disable", StringComparison.OrdinalIgnoreCase))
                        postgres.SslMode = SslMode.Disable;
                }

                return postgres.ConnectionString;
            }
            catch
            {
                return connectionString;
            }
        }
    }
}