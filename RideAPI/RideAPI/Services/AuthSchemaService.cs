using Npgsql;

namespace RideAPI.Services;

public sealed class AuthSchemaService
{
    private readonly DatabaseService _db;

    public AuthSchemaService(DatabaseService db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        foreach (var region in new[] { "NORTH", "SOUTH" })
        {
            try
            {
                await using var conn = await _db.GetConnectionAsync(region, isWrite: true);
                await EnsureRegionAsync(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: auth schema update skipped for {region}: {ex.Message}");
            }
        }
    }

    private static async Task EnsureRegionAsync(NpgsqlConnection conn)
    {
        const string sql = @"
ALTER TABLE Users ALTER COLUMN Password TYPE VARCHAR(255);

DO $$
DECLARE
    constraint_name text;
BEGIN
    FOR constraint_name IN
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        WHERE rel.relname = 'users'
          AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid) ILIKE '%role%'
    LOOP
        EXECUTE format('ALTER TABLE Users DROP CONSTRAINT IF EXISTS %I', constraint_name);
    END LOOP;
END $$;

ALTER TABLE Users
ADD CONSTRAINT users_role_binding_check
CHECK (
    (Role = 'Admin' AND CustomerID IS NULL AND DriverID IS NULL) OR
    (Role = 'Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
    (Role = 'Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email_lower ON Users (LOWER(Email));
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_phone_nonempty ON Users (Phone) WHERE Phone IS NOT NULL AND BTRIM(Phone) <> '';
CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_email_lower_nonempty ON Customers (LOWER(Email)) WHERE Email IS NOT NULL AND BTRIM(Email) <> '';
CREATE UNIQUE INDEX IF NOT EXISTS ux_customers_phone_nonempty ON Customers (Phone) WHERE Phone IS NOT NULL AND BTRIM(Phone) <> '';

CREATE TABLE IF NOT EXISTS AuthRefreshTokens (
    TokenID SERIAL PRIMARY KEY,
    UserID INT NOT NULL REFERENCES Users(UserID) ON DELETE CASCADE,
    TokenHash VARCHAR(128) NOT NULL UNIQUE,
    JwtID VARCHAR(64) NULL,
    RegionID INT NOT NULL,
    Role VARCHAR(20) NOT NULL,
    CreatedAtUtc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    ExpiresAtUtc TIMESTAMP NOT NULL,
    RevokedAtUtc TIMESTAMP NULL,
    ReplacedByTokenHash VARCHAR(128) NULL
);

CREATE INDEX IF NOT EXISTS ix_auth_refresh_tokens_user ON AuthRefreshTokens (UserID);
CREATE INDEX IF NOT EXISTS ix_auth_refresh_tokens_expiry ON AuthRefreshTokens (ExpiresAtUtc);

CREATE TABLE IF NOT EXISTS RevokedJwtTokens (
    JwtID VARCHAR(64) PRIMARY KEY,
    UserID INT NULL,
    ExpiresAtUtc TIMESTAMP NOT NULL,
    RevokedAtUtc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

DELETE FROM AuthRefreshTokens WHERE ExpiresAtUtc <= (NOW() AT TIME ZONE 'UTC');
DELETE FROM RevokedJwtTokens WHERE ExpiresAtUtc <= (NOW() AT TIME ZONE 'UTC');";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
