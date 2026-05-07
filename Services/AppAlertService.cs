namespace APP_GoiXetheoGPS.Services;

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Diagnostics;

public static class AppAlertService
{
    public static async Task ShowAsync(Page page, string title, string message, string cancel = "OK")
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var toast = Toast.Make($"{title}: {message}", ToastDuration.Long, 14);
                await toast.Show();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Alert skipped: {ex.Message}");
        }
    }
}
