using CommunityToolkit.Maui;
using APP_GoiXetheoGPS.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace APP_GoiXetheoGPS
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
    		builder.Services.AddHybridWebViewDeveloperTools();
#endif

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<HomeMapPage>();
            builder.Services.AddSingleton<TripTrackingPage>();
            builder.Services.AddSingleton<AuthPage>();
            builder.Services.AddSingleton<AuthWelcomePage>();
            builder.Services.AddSingleton<LoginPage>();
            builder.Services.AddSingleton<RegisterPage>();
            builder.Services.AddTransient<TripDetailPage>();

            builder.Services.AddSingleton<AuthSessionService>();
            builder.Services.AddSingleton<UserLocationService>();
            builder.Services.AddSingleton<ApiClient>();
            builder.Services.AddSingleton<AuthApiService>();
            builder.Services.AddSingleton<DriverApiService>();
            builder.Services.AddSingleton<RideApiService>();
            builder.Services.AddSingleton<PaymentApiService>();
            builder.Services.AddSingleton<RatingApiService>();

            return builder.Build();
        }
    }
}
