using Npgsql;
using Polly;

namespace RideAPI.Services
{
    public class DbRetryService
    {
        private readonly DatabaseService _dbService;
        private readonly ILogger<DbRetryService> _logger;

        public DbRetryService(DatabaseService dbService, ILogger<DbRetryService> logger)
        {
            _dbService = dbService;
            _logger = logger;
        }

        public async Task<T> ExecuteWithRetry<T>(Func<NpgsqlConnection, Task<T>> action, string region, bool isWrite)
        {
            var retryPolicy = Policy<T>
                .Handle<NpgsqlException>(ex => ex.IsTransient)
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => ex.Message == "MASTER_DOWN_CANNOT_WRITE" || ex.Message == "ALL_DB_NODES_DOWN")
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (result, delay, retry, _) =>
                    {
                        var message = result.Exception?.Message ?? "Unknown transient failure";
                        _logger.LogWarning("Retry {Retry} after {DelaySeconds}s: {Message}", retry, delay.TotalSeconds, message);
                    });

            return await retryPolicy.ExecuteAsync(async () =>
            {
                await using var conn = await _dbService.GetConnectionAsync(region, isWrite);
                return await action(conn);
            });
        }
    }
}