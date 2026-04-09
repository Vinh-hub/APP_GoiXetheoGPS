using MySqlConnector;

namespace RideAPI.Services
{
    public class DatabaseService
    {
        private readonly string _northConn;
        private readonly string _southConn;

        // Ghi chú:
        // - API Auth đang dùng DI: DatabaseService(IConfiguration) để đọc ConnectionStrings trong appsettings.json
        // - Phần Trip (nếu bạn không làm) có thể vẫn gọi new DatabaseService() nên cần constructor rỗng để chạy/build.
        public DatabaseService()
        {
            _northConn = "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _southConn = "Server=localhost;Database=SouthDB;User=root;Password=123456;";
        }

        public DatabaseService(IConfiguration config)
        {
            _northConn = config.GetConnectionString("NorthDB")
                ?? "Server=localhost;Database=NorthDB;User=root;Password=123456;";
            _southConn = config.GetConnectionString("SouthDB")
                ?? "Server=localhost;Database=SouthDB;User=root;Password=123456;";
        }

        public MySqlConnection GetConnection(double latitude)
        {
            // Quy ước định tuyến:
            // - latitude > 16  => NorthDB
            // - latitude <= 16 => SouthDB
            return latitude > 16
                ? new MySqlConnection(_northConn)
                : new MySqlConnection(_southConn);
        }
    }
}