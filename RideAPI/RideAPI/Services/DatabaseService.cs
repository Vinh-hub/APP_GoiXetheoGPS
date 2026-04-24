using Microsoft.Extensions.Configuration;
using Npgsql;

namespace RideAPI.Services
{
    public class DatabaseService
    {
        private readonly string _northPrimaryConn;
        private readonly string _northReplicaConn;
        private readonly string _southPrimaryConn;
        private readonly string _southReplicaConn;

        public DatabaseService(IConfiguration config)
        {
            _northPrimaryConn = GetConnectionString(config, "North", "Primary");
            _northReplicaConn = GetConnectionString(config, "North", "Replica", _northPrimaryConn);
            _southPrimaryConn = GetConnectionString(config, "South", "Primary");
            _southReplicaConn = GetConnectionString(config, "South", "Replica", _southPrimaryConn);
        }

        public static string ResolveRegionFromLatitude(double latitude)
            => LocationRoutingService.ResolveRegionFromLatitude(latitude);

        public static string ResolveRegion(double? latitude = null, string? province = null)
            => LocationRoutingService.ResolveRegion(latitude, province);

        public NpgsqlConnection GetConnection(double latitude)
            => GetConnection(latitude, province: null);

        public NpgsqlConnection GetConnection(double? latitude = null, string? province = null)
        {
            var region = ResolveRegion(latitude, province);
            var connString = region == "NORTH" ? _northPrimaryConn : _southPrimaryConn;
            return new NpgsqlConnection(connString);
        }

        public async Task<NpgsqlConnection> GetConnectionAsync(string region, bool isWrite)
        {
            var (primaryConn, replicaConn) = GetRegionConnections(region);

            if (isWrite)
            {
                var primary = await TryOpenAsync(primaryConn);
                if (primary is not null)
                    return primary;

                throw new InvalidOperationException("MASTER_DOWN_CANNOT_WRITE");
            }

            var master = await TryOpenAsync(primaryConn);
            if (master is not null)
                return master;

            var replica = await TryOpenAsync(replicaConn);
            if (replica is not null)
                return replica;

            throw new InvalidOperationException("ALL_DB_NODES_DOWN");
        }

        public async Task<NpgsqlConnection> GetConnectionAsync(double? latitude = null, string? province = null, bool isWrite = false)
        {
            var region = ResolveRegion(latitude, province);
            return await GetConnectionAsync(region, isWrite);
        }

        private (string Primary, string Replica) GetRegionConnections(string region)
        {
            return region.ToUpperInvariant() switch
            {
                "NORTH" => (_northPrimaryConn, _northReplicaConn),
                "SOUTH" => (_southPrimaryConn, _southReplicaConn),
                _ => throw new ArgumentException("Invalid region", nameof(region))
            };
        }

        private static async Task<NpgsqlConnection?> TryOpenAsync(string connectionString)
        {
            var conn = new NpgsqlConnection(connectionString);
            try
            {
                await conn.OpenAsync();
                return conn;
            }
            catch
            {
                await conn.DisposeAsync();
                return null;
            }
        }

        private static string GetConnectionString(IConfiguration config, string region, string node, string? fallback = null)
        {
            var sectionValue = config[$"DistributedDb:{region}:{node}"];
            if (!string.IsNullOrWhiteSpace(sectionValue))
                return sectionValue;

            var connectionString = config.GetConnectionString($"{region}{node}");
            if (!string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            throw new InvalidOperationException($"Missing database connection string for {region}:{node}");
        }
    }
}