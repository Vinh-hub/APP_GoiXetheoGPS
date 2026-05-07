using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using APP_GoiXetheoGPS.Services;
using Microsoft.Extensions.DependencyInjection;
using Font = Microsoft.Maui.Font;

namespace APP_GoiXetheoGPS
{
    public partial class AppShell : Shell
    {
        readonly AuthSessionService? _session;
        bool _isRouting;

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("TripDetail", typeof(Pages.TripDetailPage));
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSegmentedControl.SelectedIndex = currentTheme == AppTheme.Light ? 0 : 1;
            CurrentItem = AuthWelcomeShellContent;
            _session = MauiProgram.Services?.GetService<AuthSessionService>();
            if (_session is not null)
            {
                _session.LoginStateChanged += OnLoginStateChanged;
                _ = InitializeAuthRouteAsync();
            }
        }

        async Task InitializeAuthRouteAsync()
        {
            if (_session is null)
                return;

            await _session.RestoreAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await GoToAsync(_session.IsLoggedIn ? "//home" : "//auth-welcome");
            });
        }

        async void OnLoginStateChanged(object? sender, EventArgs e)
        {
            if (_session is null || _isRouting)
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    _isRouting = true;
                    await GoToAsync(_session.IsLoggedIn ? "//home" : "//auth-welcome");
                }
                finally
                {
                    _isRouting = false;
                }
            });
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);

            if (_session is null || _session.IsLoggedIn)
                return;

            var target = args.Target.Location.OriginalString;
            if (IsAuthRoute(target))
                return;

            args.Cancel();
            _ = MainThread.InvokeOnMainThreadAsync(async () => await GoToAsync("//auth-welcome"));
        }

        static bool IsAuthRoute(string target)
            => target.Contains("auth-welcome", StringComparison.OrdinalIgnoreCase)
               || target.Contains("auth-login", StringComparison.OrdinalIgnoreCase)
               || target.Contains("auth-register", StringComparison.OrdinalIgnoreCase);
        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }

        private void SfSegmentedControl_SelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        {
            Application.Current!.UserAppTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
        }
    }
}
