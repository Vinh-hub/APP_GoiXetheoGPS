using MySqlConnector;
using RideAPI.Models;

namespace RideAPI.Services
{
    public class TripService
    {
        DatabaseService db = new DatabaseService();

        public void BookTrip(Trip trip)
        {
            using var conn = db.GetConnection(trip.Latitude);
            conn.Open();

            var sql = @"INSERT INTO Trips(UserID, DriverID, Status, Price)
                        VALUES(@u, @d, @s, @p)";

            using var cmd = new MySqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@u", trip.UserID);
            cmd.Parameters.AddWithValue("@d", trip.DriverID);
            cmd.Parameters.AddWithValue("@s", trip.Status);
            cmd.Parameters.AddWithValue("@p", trip.Price);

            cmd.ExecuteNonQuery();
        }
    }
}