using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages
{
    public partial class MainPage : ContentPage
    {
        private List<DatabaseStatViewModel> _statsViewModels = new();

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDatabaseStatsAsync();
        }

        private async Task LoadDatabaseStatsAsync()
        {
            try
            {
                ShowLoading(true, "Đang tải dữ liệu...");
                RefreshDbButton.IsEnabled = false;

                var stats = await DistributedDatabaseService.GetDatabaseStatsAsync();

                _statsViewModels = stats
                    .Select(s => new DatabaseStatViewModel(s))
                    .ToList();

                DatabaseStatsCollection.ItemsSource = _statsViewModels;

                UpdateSummary(stats);
                UpdateDatabaseStatus(stats);

                ShowLoading(false);
            }
            catch (Exception ex)
            {
                ShowLoading(false, $"Lỗi: {ex.Message}");
                await DisplayAlert("Lỗi", $"Không thể tải dữ liệu: {ex.Message}", "OK");
            }
            finally
            {
                RefreshDbButton.IsEnabled = true;
            }
        }

        private void UpdateSummary(List<DistributedDatabaseService.DatabaseStats> stats)
        {
            if (stats == null || stats.Count == 0)
            {
                LastUpdatedLabel.Text = "Chưa cập nhật";
                TotalRecordsLabel.Text = "Tổng cộng: 0 bản ghi";
                return;
            }

            var totalRecords = stats.Sum(s => s.RecordCount);
            var lastUpdated = stats.Max(s => s.LastUpdated);

            LastUpdatedLabel.Text = $"Cập nhật lần cuối: {lastUpdated:dd MMM yyyy - HH:mm:ss}";
            TotalRecordsLabel.Text = $"Tổng cộng: {totalRecords:N0} bản ghi";
        }

        private void UpdateDatabaseStatus(List<DistributedDatabaseService.DatabaseStats> stats)
        {
            if (stats == null || stats.Count < 2)
                return;

            // DB1 always online if data returned
            Db1StatusBox.Color = Color.FromArgb("#10B981");
            Db1StatusLabel.Text = "🟢 Online";

            // DB2 logic: if record count > 0 = online
            var db2 = stats[1];
            bool db2Online = db2.RecordCount > 0;

            Db2StatusBox.Color = db2Online
                ? Color.FromArgb("#10B981")
                : Color.FromArgb("#EF4444");

            Db2StatusLabel.Text = db2Online
                ? "🟢 Online"
                : "🔴 Offline";
        }

        private void ShowLoading(bool isLoading, string message = "")
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;

            LoadingLabel.IsVisible = isLoading || !string.IsNullOrEmpty(message);
            LoadingLabel.Text = message;
        }

        private async void RefreshDbButton_OnClicked(object? sender, EventArgs e)
        {
            await RefreshAnimation();
            await LoadDatabaseStatsAsync();
        }

        private async Task RefreshAnimation()
        {
            await RefreshDbButton.ScaleTo(0.95, 100);
            await RefreshDbButton.ScaleTo(1.0, 100);
        }

        private async void TestDatabaseButton_OnClicked(object? sender, EventArgs e)
        {
            Button? testButton = sender as Button;

            try
            {
                if (testButton != null)
                {
                    testButton.IsEnabled = false;
                    testButton.Text = "Đang kiểm tra...";
                }

                ShowLoading(true, "Đang kiểm tra database...");

                var selectedDb = DatabasePicker.SelectedIndex;

                DistributedDatabaseService.DatabaseStats? selectedStat = null;

                if (selectedDb == 0)
                {
                    // DB1
                    selectedStat = await DistributedDatabaseService.GetPrimaryDatabaseStatsAsync();

                    Db1StatusBox.Color = Color.FromArgb("#10B981");
                    Db1StatusLabel.Text = "🟢 Online";

                    Db2StatusBox.Color = Color.FromArgb("#9CA3AF");
                    Db2StatusLabel.Text = "⚪ Không kiểm tra";
                }
                else
                {
                    // DB2
                    selectedStat = await DistributedDatabaseService.GetSecondaryDatabaseStatsAsync();

                    Db2StatusBox.Color = Color.FromArgb("#10B981");
                    Db2StatusLabel.Text = "🟢 Online";

                    Db1StatusBox.Color = Color.FromArgb("#9CA3AF");
                    Db1StatusLabel.Text = "⚪ Không kiểm tra";
                }

                ShowLoading(false);

                string dbName = selectedDb == 0 ? "DB1" : "DB2";

                await DisplayAlert(
                    "Kiểm tra CSDL phân tán",
                    $"{dbName}: Online",
                    "Đóng");
            }
            catch (Exception ex)
            {
                ShowLoading(false);
                await DisplayAlert("❌ Lỗi kiểm tra", ex.Message, "Đóng");
            }
            finally
            {
                if (testButton != null)
                {
                    testButton.IsEnabled = true;
                    testButton.Text = "Kiểm tra kết nối DB";
                }
            }
        }
    }

    public class DatabaseStatViewModel
    {
        private readonly DistributedDatabaseService.DatabaseStats _stat;

        public DatabaseStatViewModel(DistributedDatabaseService.DatabaseStats stat)
        {
            _stat = stat;
        }

        public string DatabaseName => _stat.DatabaseName;
        public int RecordCount => _stat.RecordCount;
        public string RecordCountDisplay => _stat.RecordCount.ToString("N0");
        public string LastUpdatedDisplay => $"Cập nhật: {_stat.LastUpdated:HH:mm:ss}";

        public string Icon => _stat.DatabaseName.Contains("Primary")
            ? "🔵"
            : "🟢";

        public Color CardBackgroundColor => _stat.DatabaseName.Contains("Primary")
            ? Color.FromArgb("#F0F9FF")
            : Color.FromArgb("#F0FDF4");

        public Color AccentColor => _stat.DatabaseName.Contains("Primary")
            ? Color.FromArgb("#0284C7")
            : Color.FromArgb("#16A34A");
    }
}