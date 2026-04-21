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
            TripsCollection.ItemsSource = await TripDataStore.GetGroupedByMonthAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Dữ liệu chuyến", ApiErrorHandler.ToUserMessage(ex), "OK");
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
            await DisplayAlertAsync("Không mở được chi tiết", ApiErrorHandler.ToUserMessage(ex), "OK");
        }
    }
}
