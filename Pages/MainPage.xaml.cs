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
            _ = RefreshRegionLabelAsync();
        }

        private void NorthRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.SetPreferredRegion("Hà Nội (North)", 21.0285);
            _ = RefreshRegionLabelAsync();
        }

        private void SouthRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.SetPreferredRegion("TP.HCM (South)", 10.8231);
            _ = RefreshRegionLabelAsync();
        }

        private void UseGpsRegionButton_OnClicked(object? sender, EventArgs e)
        {
            _locationService.ClearPreferredRegion();
            _ = RefreshRegionLabelAsync();
        }

        private async void BookTripButton_OnClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//map");
        }

        private async void ViewHistoryButton_OnClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//trips");
        }

        /// <summary>
        /// Cập nhật nhãn miền: nếu đã chọn Hà Nội/TP.HCM thì hiện tên; nếu "Theo GPS" thì suy miền từ vĩ độ (cùng ngưỡng ≥16° với API).
        /// </summary>
        async Task RefreshRegionLabelAsync()
        {
            try
            {
                var selectedName = _locationService.GetPreferredRegionName();
                if (!string.IsNullOrWhiteSpace(selectedName))
                {
                    SelectedRegionLabel.Text = $"Đang dùng: {selectedName}";
                    return;
                }

                SelectedRegionLabel.Text = "Đang dùng: Theo GPS — đang lấy vị trí…";
                var lat = await _locationService.GetCurrentLatitudeAsync();
                if (lat is double la)
                {
                    const double northThresholdDeg = 16d;
                    var region = la >= northThresholdDeg ? "Miền Bắc" : "Miền Nam";
                    SelectedRegionLabel.Text = $"Đang dùng: Theo GPS — {region} (vĩ độ ~{la:F2}°)";
                    return;
                }

                SelectedRegionLabel.Text =
                    "Đang dùng: Theo GPS — chưa có vĩ độ (bật quyền vị trí hoặc chọn Hà Nội / TP.HCM).";
            }
            catch
            {
                SelectedRegionLabel.Text = "Đang dùng: Theo GPS — không đọc được vị trí.";
            }
        }
    }
}
