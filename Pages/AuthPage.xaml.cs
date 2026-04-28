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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateJwtStatusLabel();
        _ = RefreshSessionSilentlyAsync();
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
            await this.DisplayAlertAsync("Đăng xuất", "Đã đăng xuất.", "OK");
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Đăng xuất", ApiErrorHandler.ToUserMessage(ex), "OK");
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
            var session = await _authApiService.ValidateSessionAsync();
            if (session is null)
            {
                _sessionService.Clear();
                UpdateJwtStatusLabel();
                SessionStatusLabel.Text = "Phiên không hợp lệ hoặc đã hết hạn.";
                if (showFeedback)
                    await this.DisplayAlertAsync("Phiên đăng nhập", "Phiên không hợp lệ hoặc đã hết hạn.", "OK");
                return;
            }

            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Phiên hợp lệ.";
            if (showFeedback)
                await this.DisplayAlertAsync("Phiên đăng nhập", "Phiên vẫn hợp lệ.", "OK");
        }
        catch (Exception ex)
        {
            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Không thể kiểm tra phiên lúc này.";
            if (showFeedback)
                await this.DisplayAlertAsync("Phiên đăng nhập", ApiErrorHandler.ToUserMessage(ex), "OK");
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
