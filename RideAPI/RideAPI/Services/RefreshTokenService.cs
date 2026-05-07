using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace RideAPI.Services;

public sealed class RefreshTokenService
{
    private readonly DatabaseService _db;
    private readonly JwtTokenService _jwt;

    public RefreshTokenService(DatabaseService db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<RefreshTokenResult> CreateAsync(
        string region,
        int userId,
        int regionId,
        string role,
        string jwtId,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var hash = HashToken(token);
        var expiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);

        await using var conn = await _db.GetConnectionAsync(region, isWrite: true);
        const string sql = @"
INSERT INTO AuthRefreshTokens (UserID, TokenHash, JwtID, RegionID, Role, ExpiresAtUtc)
VALUES (@userId, @hash, @jwtId, @regionId, @role, @expiresAtUtc);";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@jwtId", jwtId);
        cmd.Parameters.AddWithValue("@regionId", regionId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@expiresAtUtc", expiresAtUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new RefreshTokenResult(token, expiresAtUtc);
    }

    public async Task<StoredRefreshToken?> FindValidAsync(
        string region,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        await using var conn = await _db.GetConnectionAsync(region, isWrite: false);
        const string sql = @"
SELECT TokenID, UserID, TokenHash, JwtID, RegionID, Role, ExpiresAtUtc
FROM AuthRefreshTokens
WHERE TokenHash = @hash
  AND RevokedAtUtc IS NULL
  AND ExpiresAtUtc > (NOW() AT TIME ZONE 'UTC')
LIMIT 1;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@hash", HashToken(token));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new StoredRefreshToken(
            reader.GetInt32(reader.GetOrdinal("TokenID")),
            reader.GetInt32(reader.GetOrdinal("UserID")),
            reader.GetString(reader.GetOrdinal("TokenHash")),
            reader.IsDBNull(reader.GetOrdinal("JwtID")) ? null : reader.GetString(reader.GetOrdinal("JwtID")),
            reader.GetInt32(reader.GetOrdinal("RegionID")),
            reader.GetString(reader.GetOrdinal("Role")),
            reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc")));
    }

    public async Task RevokeAsync(
        string region,
        string refreshToken,
        string? replacedByToken = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        await using var conn = await _db.GetConnectionAsync(region, isWrite: true);
        const string sql = @"
UPDATE AuthRefreshTokens
SET RevokedAtUtc = (NOW() AT TIME ZONE 'UTC'),
    ReplacedByTokenHash = @replacedBy
WHERE TokenHash = @hash
  AND RevokedAtUtc IS NULL;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@hash", HashToken(refreshToken));
        cmd.Parameters.AddWithValue("@replacedBy", string.IsNullOrWhiteSpace(replacedByToken) ? DBNull.Value : HashToken(replacedByToken));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeJwtAsync(
        string region,
        string? jwtId,
        int? userId,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jwtId) || expiresAtUtc <= DateTime.UtcNow)
            return;

        await using var conn = await _db.GetConnectionAsync(region, isWrite: true);
        const string sql = @"
INSERT INTO RevokedJwtTokens (JwtID, UserID, ExpiresAtUtc)
VALUES (@jwtId, @userId, @expiresAtUtc)
ON CONFLICT (JwtID) DO NOTHING;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@jwtId", jwtId);
        cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@expiresAtUtc", expiresAtUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsJwtRevokedAsync(string region, string? jwtId)
    {
        if (string.IsNullOrWhiteSpace(jwtId))
            return false;

        try
        {
            await using var conn = await _db.GetConnectionAsync(region, isWrite: false);
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM RevokedJwtTokens WHERE JwtID = @jwtId AND ExpiresAtUtc > (NOW() AT TIME ZONE 'UTC')",
                conn);
            cmd.Parameters.AddWithValue("@jwtId", jwtId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record StoredRefreshToken(
    int TokenId,
    int UserId,
    string TokenHash,
    string? JwtId,
    int RegionId,
    string Role,
    DateTime ExpiresAtUtc);
