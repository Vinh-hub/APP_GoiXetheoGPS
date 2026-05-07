namespace RideAPI.Services;

public sealed class PasswordHasherService
{
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string storedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedPassword))
            return false;

        if (IsBCryptHash(storedPassword))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedPassword);
            }
            catch
            {
                return false;
            }
        }

        // Legacy compatibility: existing seed/users may still contain plaintext passwords.
        return string.Equals(password, storedPassword, StringComparison.Ordinal);
    }

    public bool IsBCryptHash(string value)
        => value.StartsWith("$2a$", StringComparison.Ordinal)
           || value.StartsWith("$2b$", StringComparison.Ordinal)
           || value.StartsWith("$2y$", StringComparison.Ordinal);
}
