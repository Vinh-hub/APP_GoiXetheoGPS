namespace APP_GoiXetheoGPS.Pages;

public partial class AuthWelcomePage : ContentPage
{
    public AuthWelcomePage()
    {
        InitializeComponent();
    }

    async void LoginButton_OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-login");
    }

    async void RegisterButton_OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//auth-register");
    }
}
