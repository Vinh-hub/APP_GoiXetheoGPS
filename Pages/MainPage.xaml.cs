using APP_GoiXetheoGPS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace APP_GoiXetheoGPS.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly UserLocationService _locationService;

        public MainPage()
        {
            InitializeComponent();

            var services = Application.Current?.Handler?.MauiContext?.Services;
            _locationService = services?.GetService<UserLocationService>() ?? new UserLocationService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateSelectedRegionLabel();
        }

        private void NorthRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.SetPreferredRegion("Hà Nội (North)", 21.0285);
            UpdateSelectedRegionLabel();
        }

        private void SouthRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.SetPreferredRegion("TP.HCM (South)", 10.8231);
            UpdateSelectedRegionLabel();
        }

        private void UseGpsRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.ClearPreferredRegion();
            UpdateSelectedRegionLabel();
        }

        private async void BookTripButton_OnClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//map");
        }

        private async void ViewHistoryButton_OnClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//trips");
        }

        private void UpdateSelectedRegionLabel()
        {
            var selectedName = _locationService.GetPreferredRegionName();
            SelectedRegionLabel.Text = string.IsNullOrWhiteSpace(selectedName)
                ? "Đang dùng: Theo GPS"
                : $"Đang dùng: {selectedName}";
        }

    }
}
