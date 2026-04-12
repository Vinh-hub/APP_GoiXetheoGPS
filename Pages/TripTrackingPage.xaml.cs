using APP_GoiXetheoGPS.Models;
using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class TripTrackingPage : ContentPage
{
    public TripTrackingPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            TripsCollection.ItemsSource = await TripDataStore.GetGroupedByMonthAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Dữ liệu chuyến", $"Không đọc được Data/trips_for_app.json.\n{ex.Message}", "OK");
        }
    }

    async void TripsCollection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView cv)
            return;

        if (e.CurrentSelection.FirstOrDefault() is not TripHistoryItem trip)
            return;

        cv.SelectedItem = null;

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
