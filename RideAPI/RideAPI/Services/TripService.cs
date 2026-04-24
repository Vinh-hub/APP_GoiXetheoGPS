using Dapper;
using RideAPI.Models;

namespace RideAPI.Services
{
    public class TripService
    {
        private readonly DatabaseService _db;
        private readonly DbRetryService _retry;

        public TripService(DatabaseService databaseService, DbRetryService retry)
        {
            _db = databaseService;
            _retry = retry;
        }

        public async Task<int> RequestTripAsync(TripRequestDto request)
        {
            var region = LocationRoutingService.ResolveRegion(request.Latitude, null);
            return await _retry.ExecuteWithRetry(async conn =>
            {
                const string sql = @"
                    INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, CreatedAt)
                    VALUES (@UserId, NULL, @Status, @Price, @StartLat, @StartLng, @EndLat, @EndLng, NOW())
                    RETURNING TripID;";

                return await conn.ExecuteScalarAsync<int>(sql, new
                {
                    UserId = request.UserID,
                    Status = "Requested",
                    request.Price,
                    StartLat = request.Latitude,
                    StartLng = request.Longitude,
                    EndLat = request.Longitude,
                    EndLng = request.Longitude
                });
            }, region, true);
        }

        public async Task<Trip?> GetTripAsync(int id, double latitude)
        {
            var region = LocationRoutingService.ResolveRegionFromLatitude(latitude);
            return await _retry.ExecuteWithRetry(async conn =>
            {
                const string sql = @"
                    SELECT TripID, UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, PaymentAmount, DriverRating, DriverComment, CreatedAt
                    FROM Trips
                    WHERE TripID = @tripId";

                return await conn.QuerySingleOrDefaultAsync<Trip>(sql, new { tripId = id });
            }, region, false);
        }

        public async Task AcceptTripAsync(AcceptTripDto request)
        {
            var region = LocationRoutingService.ResolveRegionFromLatitude(request.Latitude);
            await _retry.ExecuteWithRetry(async conn =>
            {
                const string sql = @"
                    UPDATE Trips
                    SET DriverID = @driverId, Status = 'Accepted'
                    WHERE TripID = @tripId";

                await conn.ExecuteAsync(sql, new { driverId = request.DriverID, tripId = request.TripID });
                return 0;
            }, region, true);
        }

        public async Task CompleteTripAsync(CompleteTripDto request)
        {
            var region = LocationRoutingService.ResolveRegionFromLatitude(request.Latitude);
            await _retry.ExecuteWithRetry(async conn =>
            {
                const string sql = @"
                    UPDATE Trips
                    SET Status = 'Completed', Price = @finalPrice
                    WHERE TripID = @tripId";

                await conn.ExecuteAsync(sql, new { finalPrice = request.FinalPrice, tripId = request.TripID });
                return 0;
            }, region, true);
        }
    }
}