using APP_GoiXetheoGPS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace APP_GoiXetheoGPS
{
    public partial class App : Application
    {
        DateTime _lastGpsRefreshUtc = DateTime.MinValue;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnStart()
        {
            base.OnStart();
            TryRefreshGpsOnce();
        }

        protected override void OnResume()
        {
            base.OnResume();
            TryRefreshGpsOnce();
        }

        /// <summary>OnStart + OnResume có thể gần nhau khi khởi động — gộp để vẫn chỉ đọc GPS ~một lần mỗi lần “mở” app.</summary>
        void TryRefreshGpsOnce()
        {
            var now = DateTime.UtcNow;
            if (now - _lastGpsRefreshUtc < TimeSpan.FromSeconds(3))
                return;

            _lastGpsRefreshUtc = now;

            var services = MauiProgram.Services;
            if (services is null)
                return;

            var location = services.GetService<UserLocationService>();
            if (location is null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await location.RefreshGpsOnAppLaunchAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // GPS / quyền — bỏ qua, các màn hình vẫn tự xử lý
                }
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}