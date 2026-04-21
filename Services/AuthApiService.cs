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
            _session.AccessToken = response.Token;

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
            _session.AccessToken = response.Token;

        return response;
    }

    public void Logout() => _session.Clear();

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
}
