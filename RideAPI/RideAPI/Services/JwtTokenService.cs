using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RideAPI.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public JwtTokenService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public string Issuer => _config["Jwt:Issuer"] ?? "RideAPI";
    public string Audience => _config["Jwt:Audience"] ?? "RideApp";
    public int AccessTokenMinutes => GetPositiveInt("Jwt:AccessTokenMinutes", 60);
    public int RefreshTokenDays => GetPositiveInt("Jwt:RefreshTokenDays", 14);

    public SymmetricSecurityKey GetSigningKey()
        => new(Encoding.UTF8.GetBytes(GetRequiredKey()));

    public AuthTokenResult GenerateAccessToken(
        int userId,
        string name,
        string email,
        int regionId,
        string role,
        int? customerId,
        int? driverId)
    {
        var jwtId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, name ?? string.Empty),
            new(ClaimTypes.Name, name ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new(ClaimTypes.Email, email ?? string.Empty),
            new("regionId", regionId.ToString()),
            new("role", role ?? string.Empty),
            new(ClaimTypes.Role, role ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jwtId)
        };

        if (customerId.HasValue)
            claims.Add(new Claim("customerId", customerId.Value.ToString()));
        if (driverId.HasValue)
            claims.Add(new Claim("driverId", driverId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256));

        return new AuthTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            jwtId,
            expiresAtUtc);
    }

    public string GetRequiredKey()
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            if (_env.IsDevelopment())
                return "DevelopmentOnlyJwtSigningKey_ChangeMe_AtLeast32Chars";

            throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters.");
        }

        return key;
    }

    private int GetPositiveInt(string key, int fallback)
        => int.TryParse(_config[key], out var value) && value > 0 ? value : fallback;
}

public sealed record AuthTokenResult(string Token, string JwtId, DateTime ExpiresAtUtc);
