using Polly;
using MySqlConnector;

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

        public async Task<T> ExecuteWithRetry<T>(Func<MySqlConnection, Task<T>> action, string region, bool isWrite)
        {
            var retryPolicy = Policy<T>
                .Handle<MySqlException>(ex => ex.IsTransient)
                .Or<TimeoutException>()
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
                using var conn = await _dbService.GetConnectionAsync(region, isWrite);
                return await action(conn);
            });
        }
    }
}