using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class AuthPage : ContentPage
{
    readonly AuthApiService _authApiService;
    readonly AuthSessionService _sessionService;

    public AuthPage(AuthApiService authApiService, AuthSessionService sessionService)
    {
        InitializeComponent();
        _authApiService = authApiService;
        _sessionService = sessionService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateJwtStatus();
        _ = ValidateAndRenderSessionAsync();
    }

    async void LoginButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            LoginButton.IsEnabled = false;
            LoginButton.Text = "Đang đăng nhập...";

            var email = LoginEmailEntry.Text?.Trim() ?? string.Empty;
            var password = LoginPasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlertAsync("Đăng nhập", "Vui lòng nhập email và mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.LoginAsync(email, password);
            UpdateJwtStatus();
            await ValidateAndRenderSessionAsync();

            await DisplayAlertAsync(
                "Đăng nhập thành công",
                result?.Message ?? "Đã lưu JWT cho các API cần xác thực.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Đăng nhập", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Đăng nhập";
        }
    }

    async void RegisterButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            RegisterButton.IsEnabled = false;
            RegisterButton.Text = "Đang đăng ký...";

            var request = new AuthApiService.RegisterRequest(
                RegisterNameEntry.Text?.Trim() ?? string.Empty,
                RegisterPhoneEntry.Text?.Trim() ?? string.Empty,
                RegisterEmailEntry.Text?.Trim() ?? string.Empty,
                RegisterPasswordEntry.Text ?? string.Empty);

            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrWhiteSpace(request.Password))
            {
                await DisplayAlertAsync("Đăng ký", "Vui lòng nhập đủ họ tên, email, mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.RegisterAsync(request);
            UpdateJwtStatus();
            await ValidateAndRenderSessionAsync();

            await DisplayAlertAsync(
                "Đăng ký thành công",
                result?.Message ?? "Tài khoản đã được tạo và JWT đã lưu.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Đăng ký", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Text = "Đăng ký";
        }
    }

    async void LogoutButton_OnClicked(object? sender, EventArgs e)
    {
        await _authApiService.LogoutAsync();
        UpdateJwtStatus();
        SessionStatusLabel.Text = "Đã đăng xuất.";
        await DisplayAlertAsync("Đăng xuất", "Đã đăng xuất.", "OK");
    }

    async void CheckSessionButton_OnClicked(object? sender, EventArgs e)
    {
        await ValidateAndRenderSessionAsync(showAlert: true);
    }

    void UpdateJwtStatus()
    {
        var token = _sessionService.AccessToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            JwtStatusLabel.Text = "Chưa đăng nhập";
            return;
        }

        var expiry = _sessionService.GetTokenExpiryUtc();
        var expiryText = expiry.HasValue
            ? expiry.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            : "không xác định";

        JwtStatusLabel.Text = $"Đã có JWT (hết hạn: {expiryText})";
    }

    async Task ValidateAndRenderSessionAsync(bool showAlert = false)
    {
        try
        {
            CheckSessionButton.IsEnabled = false;
            var session = await _authApiService.ValidateSessionAsync();
            if (session is null)
            {
                SessionStatusLabel.Text = "Phiên không hợp lệ hoặc đã hết hạn.";
                UpdateJwtStatus();
                if (showAlert)
                    await DisplayAlertAsync("Phiên đăng nhập", "Phiên không hợp lệ. Vui lòng đăng nhập lại.", "OK");
                return;
            }

            var name = string.IsNullOrWhiteSpace(session.Name) ? $"User #{session.UserId}" : session.Name;
            SessionStatusLabel.Text = $"Phiên hợp lệ: {name} - {session.Role} - vùng {session.RegionId}";
            if (showAlert)
                await DisplayAlertAsync("Phiên đăng nhập", "JWT hợp lệ, phiên đang hoạt động.", "OK");
        }
        catch (Exception ex)
        {
            SessionStatusLabel.Text = "Không thể kiểm tra phiên.";
            if (showAlert)
                await DisplayAlertAsync("Phiên đăng nhập", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            CheckSessionButton.IsEnabled = true;
        }
    }
}
