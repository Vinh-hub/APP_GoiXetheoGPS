using Npgsql;
using RideAPI.Models;

namespace RideAPI.Services
{
    public class TripService
    {
        private readonly DatabaseService db;

        public TripService(DatabaseService databaseService)
        {
            db = databaseService;
        }

        public void RequestTrip(TripRequestDto request)
        {
            using var conn = db.GetConnection(request.Latitude);
            conn.Open();

            var sql = @"INSERT INTO Trips (UserID, DriverID, Status, Price, CreatedAt)
                        VALUES (@userId, NULL, @status, @price, NOW())";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", request.UserID);
            cmd.Parameters.AddWithValue("@status", "Requested");
            cmd.Parameters.AddWithValue("@price", request.Price);

            cmd.ExecuteNonQuery();
        }

        public Trip? GetTrip(int id, double latitude)
        {
            using var conn = db.GetConnection(latitude);
            conn.Open();

            var sql = @"SELECT TripID, UserID, DriverID, Status, Price, CreatedAt
                        FROM Trips
                        WHERE TripID = @tripId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tripId", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Trip
                {
                    TripID = Convert.ToInt32(reader["TripID"]),
                    UserID = Convert.ToInt32(reader["UserID"]),
                    DriverID = reader["DriverID"] is DBNull ? 0 : Convert.ToInt32(reader["DriverID"]),
                    Status = Convert.ToString(reader["Status"]) ?? string.Empty,
                    Price = Convert.ToDecimal(reader["Price"]),
                    StartLat = latitude,
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }

            return null;
        }

        public void AcceptTrip(AcceptTripDto request)
        {
            using var conn = db.GetConnection(request.Latitude);
            conn.Open();

            var sql = @"UPDATE Trips
                        SET DriverID = @driverId, Status = 'Accepted'
                        WHERE TripID = @tripId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@driverId", request.DriverID);
            cmd.Parameters.AddWithValue("@tripId", request.TripID);

            cmd.ExecuteNonQuery();
        }

        public void CompleteTrip(CompleteTripDto request)
        {
            using var conn = db.GetConnection(request.Latitude);
            conn.Open();

            var sql = @"UPDATE Trips
                        SET Status = 'Completed', Price = @finalPrice
                        WHERE TripID = @tripId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@finalPrice", request.FinalPrice);
            cmd.Parameters.AddWithValue("@tripId", request.TripID);

            cmd.ExecuteNonQuery();
        }
    }
}