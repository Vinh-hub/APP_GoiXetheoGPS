using MySqlConnector;

namespace RideAPI.Services
{
    public class DatabaseService
    {
        private string northConn =
            "Server=localhost;Database=NorthDB;User=root;Password=123456;";

        private string southConn =
            "Server=localhost;Database=SouthDB;User=root;Password=123456;";

        public MySqlConnection GetConnection(double latitude)
        {
            return latitude > 16
                ? new MySqlConnection(northConn)
                : new MySqlConnection(southConn);
        }
    }
}