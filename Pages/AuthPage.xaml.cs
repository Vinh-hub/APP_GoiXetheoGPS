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
                await DisplayAlert("Đăng nhập", "Vui lòng nhập email và mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.LoginAsync(email, password);
            UpdateJwtStatus();

            await DisplayAlert(
                "Đăng nhập thành công",
                result?.Message ?? "Đã lưu JWT cho các API cần xác thực.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Đăng nhập", ApiErrorHandler.ToUserMessage(ex), "OK");
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
                await DisplayAlert("Đăng ký", "Vui lòng nhập đủ họ tên, email, mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.RegisterAsync(request);
            UpdateJwtStatus();

            await DisplayAlert(
                "Đăng ký thành công",
                result?.Message ?? "Tài khoản đã được tạo và JWT đã lưu.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Đăng ký", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Text = "Đăng ký";
        }
    }

    async void LogoutButton_OnClicked(object? sender, EventArgs e)
    {
        _authApiService.Logout();
        UpdateJwtStatus();
        await DisplayAlert("Đăng xuất", "Đã xóa JWT đăng nhập.", "OK");
    }

    void UpdateJwtStatus()
    {
        var token = _sessionService.AccessToken;
        JwtStatusLabel.Text = string.IsNullOrWhiteSpace(token)
            ? "Chưa đăng nhập"
            : $"Đã có JWT (độ dài: {token.Length})";
    }
}
