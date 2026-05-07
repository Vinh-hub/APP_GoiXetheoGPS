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
                || string.IsNullOrWhiteSpace(request.Phone)
                || string.IsNullOrWhiteSpace(request.Password))
            {
                await AppAlertService.ShowAsync(this, "Đăng ký", "Vui lòng nhập đủ họ tên, số điện thoại, email, mật khẩu.");
                return;
            }

            if (!request.Email.Contains('@') || request.Password.Length < 6)
            {
                await AppAlertService.ShowAsync(this, "Đăng ký", "Email không hợp lệ hoặc mật khẩu dưới 6 ký tự.");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^\+?[0-9]{9,15}$"))
            {
                await AppAlertService.ShowAsync(this, "Đăng ký", "Số điện thoại không hợp lệ.");
                return;
            }

            var result = await _authApiService.RegisterAsync(request);
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
            {
                await AppAlertService.ShowAsync(this, "Đăng ký", "Đăng ký thất bại. Vui lòng thử lại.");
                return;
            }

            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await AppAlertService.ShowAsync(this, "Đăng ký", ApiErrorHandler.ToUserMessage(ex));
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Text = "Đăng ký";
        }
    }
}
