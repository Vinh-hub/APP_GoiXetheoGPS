using APP_GoiXetheoGPS.Services;
using System.Globalization;

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

        // Subscribe to login state changes
        _sessionService.LoginStateChanged += OnLoginStateChanged;
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateJwtStatusLabel();
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Always refresh UI when page appears
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateJwtStatusLabel();
            _ = RefreshSessionSilentlyAsync();
        });
    }

    async void CheckSessionButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            CheckSessionButton.IsEnabled = false;
            CheckSessionButton.Text = "Đang kiểm tra...";
            await ValidateAndRenderSessionAsync(showFeedback: true);
        }
        finally
        {
            CheckSessionButton.IsEnabled = true;
            CheckSessionButton.Text = "Kiểm tra phiên";
        }
    }

    async void LogoutButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            LogoutButton.IsEnabled = false;
            LogoutButton.Text = "Đang đăng xuất...";
            await _authApiService.LogoutAsync();
            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Phiên đã được đăng xuất.";
            await AppAlertService.ShowAsync(this, "Đăng xuất", "Đã đăng xuất.");
            await Shell.Current.GoToAsync("//auth-welcome");
        }
        catch (Exception ex)
        {
            await AppAlertService.ShowAsync(this, "Đăng xuất", ApiErrorHandler.ToUserMessage(ex));
        }
        finally
        {
            LogoutButton.IsEnabled = true;
            LogoutButton.Text = "Đăng xuất";
        }
    }

    async Task ValidateAndRenderSessionAsync()
        => await ValidateAndRenderSessionAsync(showFeedback: false);

    async Task RefreshSessionSilentlyAsync()
    {
        try
        {
            await ValidateAndRenderSessionAsync(showFeedback: false);
        }
        catch
        {
            SessionStatusLabel.Text = "Không thể kiểm tra phiên lúc này.";
        }
    }

    async Task ValidateAndRenderSessionAsync(bool showFeedback)
    {
        try
        {
            // First check if token is expired before calling API
            var token = _sessionService.AccessToken;
            var expiresUtc = _sessionService.GetTokenExpiryUtc();

            if (string.IsNullOrWhiteSpace(token))
            {
                SessionStatusLabel.Text = "Chưa đăng nhập.";
                if (showFeedback)
                    await AppAlertService.ShowAsync(this, "Phiên đăng nhập", "Bạn chưa đăng nhập.");
                return;
            }

            if (expiresUtc.HasValue && expiresUtc.Value <= DateTimeOffset.UtcNow)
            {
                _sessionService.Clear();
                UpdateJwtStatusLabel();
                SessionStatusLabel.Text = "Phiên đã hết hạn.";
                if (showFeedback)
                    await AppAlertService.ShowAsync(this, "Phiên đăng nhập", "Phiên đã hết hạn. Vui lòng đăng nhập lại.");
                return;
            }

            var session = await _authApiService.ValidateSessionAsync();
            if (session is null)
            {
                _sessionService.Clear();
                UpdateJwtStatusLabel();
                SessionStatusLabel.Text = "Phiên không hợp lệ.";
                if (showFeedback)
                    await AppAlertService.ShowAsync(this, "Phiên đăng nhập", "Phiên không hợp lệ. Vui lòng đăng nhập lại.");
                return;
            }

            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Phiên hợp lệ.";
            if (showFeedback)
                await AppAlertService.ShowAsync(this, "Phiên đăng nhập", "Phiên vẫn hợp lệ.");
        }
        catch (Exception ex)
        {
            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Không thể kiểm tra phiên lúc này.";
            if (showFeedback)
                await AppAlertService.ShowAsync(this, "Phiên đăng nhập", ApiErrorHandler.ToUserMessage(ex));
        }
    }

    void UpdateJwtStatusLabel()
    {
        var token = _sessionService.AccessToken;
        JwtStatusLabel.Text = string.IsNullOrWhiteSpace(token)
            ? "Chưa đăng nhập"
            : "Đã đăng nhập";

        var expiresUtc = _sessionService.GetTokenExpiryUtc();
        if (expiresUtc is null)
        {
            ExpiryStatusLabel.Text = "Hạn dùng: Không xác định";
            return;
        }

        var expiresLocal = expiresUtc.Value.ToLocalTime();
        var state = expiresUtc.Value <= DateTimeOffset.UtcNow ? "đã hết hạn" : "còn hiệu lực";
        ExpiryStatusLabel.Text =
            $"Hạn dùng: {expiresLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)} ({state})";
    }
}
