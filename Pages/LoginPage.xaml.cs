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

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(password))
            {
                await AppAlertService.ShowAsync(this, "Đăng nhập", "Vui lòng nhập email hợp lệ và mật khẩu.");
                return;
            }

            var result = await _authApiService.LoginAsync(email, password);
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
            {
                // Lỗi mạng / body rỗng / deserialize lệch — không đồng nhất với “sai mật khẩu” (401 thường ném exception kèm message từ API).
                var hint = string.IsNullOrWhiteSpace(result?.Message)
                    ? "Không nhận được phiên đăng nhập. Kiểm tra API đang chạy và thử lại."
                    : result!.Message;
                await AppAlertService.ShowAsync(this, "Đăng nhập", hint);
                return;
            }

            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await AppAlertService.ShowAsync(this, "Đăng nhập", ApiErrorHandler.ToUserMessage(ex));
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Đăng nhập";
        }
    }
}
