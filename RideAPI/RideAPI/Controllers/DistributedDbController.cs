using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RideAPI.Services;

namespace RideAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/distributed-db")]
    [Route("api/database")]
    [Route("api/distributeddb")]
    public class DistributedDbController : ControllerBase
    {
        private readonly DatabaseService _db;

        public DistributedDbController(DatabaseService db)
        {
            _db = db;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var north = await GetRegionStatsAsync("NORTH", "DB1 (Primary)");
            var south = await GetRegionStatsAsync("SOUTH", "DB2 (Replica)");

            return Ok(new[] { north, south });
        }

        [HttpGet("stats/primary")]
        public async Task<IActionResult> GetPrimaryStats()
        {
            var stat = await GetRegionStatsAsync("NORTH", "DB1 (Primary)");
            return Ok(stat);
        }

        [HttpGet("stats/secondary")]
        [HttpGet("stats/replica")]
        public async Task<IActionResult> GetSecondaryStats()
        {
            var stat = await GetRegionStatsAsync("SOUTH", "DB2 (Replica)");
            return Ok(stat);
        }

        private async Task<DatabaseStatsDto> GetRegionStatsAsync(string region, string displayName)
        {
            try
            {
                await using var conn = await _db.GetConnectionAsync(region, isWrite: false);
                await conn.OpenAsync();

                var count = await ReadCountAsync(conn);
                var lastUpdated = await ReadLastUpdatedAsync(conn);

                return new DatabaseStatsDto
                {
                    DatabaseName = displayName,
                    RecordCount = count,
                    LastUpdated = lastUpdated
                };
            }
            catch
            {
                return new DatabaseStatsDto
                {
                    DatabaseName = displayName,
                    RecordCount = 0,
                    LastUpdated = DateTime.Now
                };
            }
        }

        private static async Task<int> ReadCountAsync(NpgsqlConnection conn)
        {
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Users", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private static async Task<DateTime> ReadLastUpdatedAsync(NpgsqlConnection conn)
        {
            const string sql = @"SELECT COALESCE(MAX(CreatedAt), NOW()) FROM Users";
            await using var cmd = new NpgsqlCommand(sql, conn);
            var value = await cmd.ExecuteScalarAsync();
            return value is DateTime dt ? dt : DateTime.Now;
        }

        public sealed class DatabaseStatsDto
        {
            public string DatabaseName { get; set; } = string.Empty;
            public int RecordCount { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}
