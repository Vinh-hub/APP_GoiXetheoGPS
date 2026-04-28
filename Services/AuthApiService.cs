namespace APP_GoiXetheoGPS.Services;

public sealed class AuthApiService
{
    readonly ApiClient _api;
    readonly AuthSessionService _session;

    public AuthApiService(ApiClient api, AuthSessionService session)
    {
        _api = api;
        _session = session;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _api.PostAsync<LoginRequest, AuthResponse>(
            "/api/auth/login",
            new LoginRequest(email, password),
            requiresAuth: false,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(response?.Token))
            _session.SaveLogin(response);

        return response;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _api.PostAsync<RegisterRequest, AuthResponse>(
            "/api/auth/register",
            request,
            requiresAuth: false,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(response?.Token))
            _session.SaveLogin(response);

        return response;
    }

    public async Task<SessionResponse?> ValidateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_session.IsLoggedIn)
        {
            _session.Clear();
            return null;
        }

        try
        {
            var session = await _api.GetAsync<SessionResponse>("/api/auth/session", requiresAuth: true, cancellationToken);
            if (session is null || !session.IsAuthenticated)
            {
                _session.Clear();
                return null;
            }

            _session.UserId = session.UserId;
            _session.Role = session.Role ?? string.Empty;
            _session.Name = session.Name ?? string.Empty;
            _session.Email = session.Email ?? string.Empty;
            _session.RegionId = session.RegionId;
            return session;
        }
        catch
        {
            _session.Clear();
            return null;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _api.PostAsync<object>("/api/auth/logout", new { }, requiresAuth: true, cancellationToken);
        }
        catch
        {
            // Ignore API logout errors and clear local session anyway.
        }

        _session.Clear();
    }

    public sealed record LoginRequest(string Email, string Password);

    public sealed record RegisterRequest(string Name, string Phone, string Email, string Password);

    public sealed class AuthResponse
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
        public int UserId { get; set; }
        public string? Role { get; set; }
        public int? CustomerId { get; set; }
        public int? DriverId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int RegionId { get; set; }
    }

    public sealed class SessionResponse
    {
        public bool IsAuthenticated { get; set; }
        public int UserId { get; set; }
        public string? Role { get; set; }
        public int? CustomerId { get; set; }
        public int? DriverId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public int RegionId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
