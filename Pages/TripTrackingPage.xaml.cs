using APP_GoiXetheoGPS.Models;
using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class TripTrackingPage : ContentPage
{
    private readonly TripHistoryService _tripHistoryService;

    public TripTrackingPage()
    {
        InitializeComponent();

        var settingsService = ServiceHelper.GetService<AppSettingsService>() ?? new AppSettingsService();
        var rideApiService = ServiceHelper.GetService<RideApiService>() ?? new RideApiService(settingsService);
        _tripHistoryService = ServiceHelper.GetService<TripHistoryService>()
            ?? new TripHistoryService(settingsService, rideApiService);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var result = await _tripHistoryService.GetPreferredHistoryAsync();
            TripsCollection.ItemsSource = result.Groups;
            SourceHintLabel.Text = result.SourceMessage;
        }
        catch (Exception ex)
        {
            SourceHintLabel.Text = "Không tải được dữ liệu chuyến.";
            await DisplayAlertAsync("Dữ liệu chuyến", ex.Message, "OK");
        }
    }

    async void TripsCollection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView)
            return;

        if (e.CurrentSelection.FirstOrDefault() is not TripHistoryItem trip)
            return;

        collectionView.SelectedItem = null;

        try
        {
            await Shell.Current.GoToAsync($"TripDetail?tripId={Uri.EscapeDataString(trip.Id)}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Không mở được chi tiết", ex.Message, "OK");
        }
    }
}
