using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class RegisterPage : ContentPage
{
    readonly AuthApiService _authApiService;

    public RegisterPage(AuthApiService authApiService)
    {
        InitializeComponent();
        _authApiService = authApiService;
    }

    async void BackButton_OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-welcome");
    }

    async void GoLogin_Tapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-login");
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
                await this.DisplayAlertAsync("Đăng ký", "Vui lòng nhập đủ họ tên, email, mật khẩu.", "OK");
                return;
            }

            var result = await _authApiService.RegisterAsync(request);
            await this.DisplayAlertAsync(
                "Đăng ký thành công",
                result?.Message ?? "Tạo tài khoản thành công.",
                "OK");

            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Đăng ký", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Text = "Đăng ký";
        }
    }
}
