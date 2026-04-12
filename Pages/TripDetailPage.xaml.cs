using System.Globalization;
using APP_GoiXetheoGPS.Models;
using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

[QueryProperty(nameof(TripId), "tripId")]
public partial class TripDetailPage : ContentPage
{
    string tripId = string.Empty;

    public string TripId
    {
        get => tripId;
        set
        {
            tripId = value;
            _ = RefreshFromTripIdAsync();
        }
    }

    public TripDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshFromTripIdAsync();
    }

    async Task RefreshFromTripIdAsync()
    {
        var id = string.IsNullOrEmpty(tripId) ? null : Uri.UnescapeDataString(tripId);
        TripHistoryItem? trip = null;
        try
        {
            trip = await TripDataStore.FindByIdAsync(id);
        }
        catch
        {
            // ignore
        }

        await MainThread.InvokeOnMainThreadAsync(() => ApplyTrip(trip));
    }

    void ApplyTrip(TripHistoryItem? trip)
    {
        if (trip is null)
        {
            RouteLabel.Text = "Không tìm thấy chuyến.";
            WhenLabel.Text = string.Empty;
            DriverNameLabel.Text = "—";
            VehicleLabel.Text = string.Empty;
            PriceLabel.Text = "—";
            StatusLabel.Text = "—";
            StatusBadge.BackgroundColor = Color.FromArgb("#E5E7EB");
            return;
        }

        RouteLabel.Text = trip.RouteLine;
        WhenLabel.Text = trip.WhenDisplay;
        DriverNameLabel.Text = trip.DriverName;
        VehicleLabel.Text = trip.VehicleInfo;
        // Giống app be: chuyến hủy / hoàn thành vẫn xem được giá (ước tính hoặc phí). Chỉ chuyến đang chạy mới "chưa có giá".
        PriceLabel.Text = trip.Status == "Đang chạy" && trip.PriceVnd <= 0
            ? "Đang cập nhật"
            : string.Format(new CultureInfo("vi-VN"), "{0:N0} đ", trip.PriceVnd);
        StatusLabel.Text = trip.Status;

        var (bg, fg) = trip.Status switch
        {
            "Hoàn thành" => (Color.FromArgb("#D1FAE5"), Color.FromArgb("#065F46")),
            "Đang chạy" => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF")),
            "Đã hủy" => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#4B5563")),
            _ => (Color.FromArgb("#E0E7FF"), Color.FromArgb("#312E81")),
        };

        StatusBadge.BackgroundColor = bg;
        StatusLabel.TextColor = fg;
    }
}
