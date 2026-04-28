using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class LoginPage : ContentPage
{
    readonly AuthApiService _authApiService;

    public LoginPage(AuthApiService authApiService)
    {
        InitializeComponent();
        _authApiService = authApiService;
    }

    async void BackButton_OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-welcome");
    }

    async void GoRegister_Tapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-register");
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
                await this.DisplayAlertAsync("Đăng nhập", "Vui lòng nhập email và mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.LoginAsync(email, password);
            await this.DisplayAlertAsync(
                "Đăng nhập thành công",
                result?.Message ?? "Đăng nhập thành công.",
                "OK");

            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Đăng nhập", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Đăng nhập";
        }
    }
}
