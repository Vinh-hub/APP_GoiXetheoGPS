using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace RideAPI.Services
{
    /// <summary>
    /// Mô hình triển khai mặc định: một MySQL (XAMPP), hai schema NorthDB / SouthDB.
    /// Master và Replica dùng chung host nếu không ghi ConnectionStrings đầy đủ — failover vẫn do code
    /// (đọc: thử master rồi replica; ghi: chỉ master, lỗi nếu master không ping được).
    /// Để demo failover đọc sang “replica” khác: đặt NorthReplica / SouthReplica thành chuỗi kết nối thật (máy khác hoặc user read-only).
    /// </summary>
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

            _northMasterConn = EffectiveConn(config.GetConnectionString("NorthMaster")) ?? BuildConn(northDb);
            _southMasterConn = EffectiveConn(config.GetConnectionString("SouthMaster")) ?? BuildConn(southDb);
            _northReplicaConn = EffectiveConn(config.GetConnectionString("NorthReplica")) ?? _northMasterConn;
            _southReplicaConn = EffectiveConn(config.GetConnectionString("SouthReplica")) ?? _southMasterConn;
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