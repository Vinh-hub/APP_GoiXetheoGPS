using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace RideAPI.Services
{
    public class DatabaseService
    {
        private readonly string _northMasterConn;
        private readonly string _northReplicaConn;
        private readonly string _southMasterConn;
        private readonly string _southReplicaConn;

        public DatabaseService()
        {
            _northMasterConn = "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _northReplicaConn = "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _southMasterConn = "Server=localhost;Database=SouthDB;User=root;Password=123456;";
            _southReplicaConn = "Server=localhost;Database=SouthDB;User=root;Password=123456;";
        }

        public DatabaseService(IConfiguration config)
        {
            _northMasterConn = config.GetConnectionString("NorthMaster")
                ?? "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _northReplicaConn = config.GetConnectionString("NorthReplica")
                ?? "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _southMasterConn = config.GetConnectionString("SouthMaster")
                ?? "Server=localhost;Database=SouthDB;User=root;Password=123456;";
            _southReplicaConn = config.GetConnectionString("SouthReplica")
                ?? "Server=localhost;Database=SouthDB;User=root;Password=123456;";
        }

        // Phương thức async dùng cho DbRetryService (hỗ trợ master/replica & failover)
        public async Task<MySqlConnection> GetConnectionAsync(string region, bool isWrite)
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
                var conn = new MySqlConnection(masterConn);
                if (await IsConnectionAlive(conn))
                    return conn;
                throw new Exception("MASTER_DOWN_CANNOT_WRITE");
            }
            else
            {
                var master = new MySqlConnection(masterConn);
                if (await IsConnectionAlive(master))
                    return master;
                var replica = new MySqlConnection(replicaConn);
                if (await IsConnectionAlive(replica))
                    return replica;
                throw new Exception("ALL_DB_NODES_DOWN");
            }
        }

        // Phương thức cũ giữ lại cho tương thích (đồng bộ, trả connection chưa mở)
        public MySqlConnection GetConnection(double latitude)
        {
            return latitude > 16
                ? new MySqlConnection(_northMasterConn)
                : new MySqlConnection(_southMasterConn);
        }

        private async Task<bool> IsConnectionAlive(MySqlConnection conn)
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
    }
}