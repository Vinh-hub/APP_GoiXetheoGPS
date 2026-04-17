using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using APP_GoiXetheoGPS.Configuration;
using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages
{
    public partial class HomeMapPage : ContentPage
    {
        private double _gpsButtonStartY;

        // HttpClient dùng chung cho tính năng gợi ý địa điểm (Mapbox Geocoding).
        private static readonly HttpClient Http = new();

        // Trạng thái chọn loại điểm hiện tại (đón / đến) khi người dùng chạm bản đồ.
        private bool _placingPickup = true;

        // Tọa độ đã chọn (có thể đến từ: chạm bản đồ hoặc chọn gợi ý khi nhập chữ).
        private double? _pickupLat;
        private double? _pickupLng;
        private double? _dropLat;
        private double? _dropLng;

        // Hủy request autocomplete cũ khi người dùng gõ tiếp (tránh spam API).
        private CancellationTokenSource? _suggestCts;

        // Chống vòng lặp: khi mình set text Entry theo gợi ý thì không gọi lại TextChanged.
        private bool _suppressTextEvents;

        // Entry đang được thao tác (để biết gợi ý chọn xong sẽ set cho điểm đón hay điểm đến).
        private enum ActiveField { Pickup, Dropoff }
        private ActiveField _activeField = ActiveField.Pickup;

        // Model gợi ý địa điểm (tên hiển thị + tọa độ).
        private sealed record MapboxSuggestion(string Name, double Lat, double Lng);

        public HomeMapPage()
        {
            InitializeComponent();
            SuggestionsList.ItemsSource = new List<MapboxSuggestion>();
        }

        private void BtnPickup_OnClicked(object? sender, EventArgs e)
        {
            _placingPickup = true;
            _activeField = ActiveField.Pickup;
            ApplySelectionStyle();
            SetActiveEntryVisibility();
            PickupEntry.Focus();
        }

        private void BtnDropoff_OnClicked(object? sender, EventArgs e)
        {
            _placingPickup = false;
            _activeField = ActiveField.Dropoff;
            ApplySelectionStyle();
            SetActiveEntryVisibility();
            DropoffEntry.Focus();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ApplySelectionStyle();
            SetActiveEntryVisibility();
            _ = MapResizeAfterLayoutAsync();
        }

        /// <summary>
        /// Sau khi Shell đo layout xong, WebView trên Android thường cần map.resize() mới vẽ tile.
        /// </summary>
        private async Task MapResizeAfterLayoutAsync()
        {
            try
            {
                await Task.Delay(600);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await MapHybrid.EvaluateJavaScriptAsync(
                            "if(typeof window.__mapboxResizeMap==='function')window.__mapboxResizeMap();");
                    }
                    catch
                    {
                        /* map chưa init */
                    }
                });
            }
            catch
            {
                /* ignore */
            }
        }

        private void ApplySelectionStyle()
        {
            // Màu nút theo trạng thái chọn: xanh = đang chọn, xám = không chọn.
            var active = Color.FromArgb("#2D7DFF");
            var inactive = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#3A3A3C")
                : Color.FromArgb("#E9ECEF");

            BtnPickup.BackgroundColor = _placingPickup ? active : inactive;
            BtnDropoff.BackgroundColor = !_placingPickup ? active : inactive;
            BtnPickup.TextColor = Colors.White;
            BtnDropoff.TextColor = Colors.White;
        }

        private void SetActiveEntryVisibility()
        {
            PickupEntryContainer.IsVisible = _activeField == ActiveField.Pickup;
            DropoffEntryContainer.IsVisible = _activeField == ActiveField.Dropoff;
        }

        private void PickupEntry_OnFocused(object? sender, FocusEventArgs e)
        {
            _placingPickup = true;
            _activeField = ActiveField.Pickup;
            ApplySelectionStyle();
            SetActiveEntryVisibility();
        }

        private void DropoffEntry_OnFocused(object? sender, FocusEventArgs e)
        {
            _placingPickup = false;
            _activeField = ActiveField.Dropoff;
            ApplySelectionStyle();
            SetActiveEntryVisibility();
        }

        private async void PickupEntry_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            _activeField = ActiveField.Pickup;
            await UpdateSuggestionsAsync(e.NewTextValue);
        }

        private async void DropoffEntry_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            _activeField = ActiveField.Dropoff;
            await UpdateSuggestionsAsync(e.NewTextValue);
        }

        private async void PickupEntry_OnCompleted(object? sender, EventArgs e)
        {
            _activeField = ActiveField.Pickup;
            await SearchNamedPlaceForActiveFieldAsync(PickupEntry.Text);
        }

        private async void DropoffEntry_OnCompleted(object? sender, EventArgs e)
        {
            _activeField = ActiveField.Dropoff;
            await SearchNamedPlaceForActiveFieldAsync(DropoffEntry.Text);
        }

        private async void PickupSearch_OnClicked(object? sender, EventArgs e)
        {
            _activeField = ActiveField.Pickup;
            await SearchNamedPlaceForActiveFieldAsync(PickupEntry.Text);
        }

        private async void DropoffSearch_OnClicked(object? sender, EventArgs e)
        {
            _activeField = ActiveField.Dropoff;
            await SearchNamedPlaceForActiveFieldAsync(DropoffEntry.Text);
        }

        /// <summary>
        /// Gọi Mapbox, hiển thị danh sách nhiều địa điểm (vd: gõ Vincom / vincome) để khách chạm chọn — không tự chọn dòng đầu.
        /// </summary>
        private async Task SearchNamedPlaceForActiveFieldAsync(string? query)
        {
            query = (query ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                await DisplayAlertAsync("Tìm kiếm", "Nhập tên địa điểm (ít nhất 2 ký tự).", "OK");
                return;
            }

            if (!MapboxConfig.TryGetAccessToken(out var geocodeToken) ||
                !MapboxConfig.LooksLikeMapboxPublicToken(geocodeToken))
            {
                await DisplayAlertAsync(
                    "Thiếu Mapbox token",
                    "Vào tab Bản đồ và nhập public token (pk...) khi được hỏi, hoặc tạo Resources/Raw/mapbox_token.txt — xem mapbox_token.sample.txt.",
                    "OK");
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                // autocomplete=true + nhiều limit: phù hợp gõ tắt / gần đúng, nhiều chi nhánh Vincom trong VN.
                var items = await GeocodeAsync(query, cts.Token, limit: 10, autocomplete: true);
                if (items.Count == 0)
                {
                    await DisplayAlertAsync(
                        "Không tìm thấy",
                        $"Không có kết quả cho \"{query}\". Thử tên khác hoặc thêm quận / thành phố.",
                        "OK");
                    SetSuggestions(Array.Empty<MapboxSuggestion>());
                    return;
                }

                SetSuggestions(items);
                if (_activeField == ActiveField.Pickup)
                    PickupEntry.Unfocus();
                else
                    DropoffEntry.Unfocus();
            }
            catch (OperationCanceledException)
            {
                await DisplayAlertAsync("Tìm kiếm", "Hết thời gian chờ. Thử lại.", "OK");
            }
            catch (HttpRequestException)
            {
                await DisplayAlertAsync("Tìm kiếm", "Không gọi được Mapbox. Kiểm tra mạng hoặc token.", "OK");
            }
            catch
            {
                await DisplayAlertAsync("Tìm kiếm", "Có lỗi khi tìm địa điểm. Thử lại.", "OK");
            }
        }

        private void GpsButton_OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not VisualElement btn)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _gpsButtonStartY = btn.TranslationY;
                    break;

                case GestureStatus.Running:
                {
                    // Only allow vertical dragging within the page bounds.
                    var proposed = _gpsButtonStartY + e.TotalY;

                    // Keep some padding from top/bottom edges.
                    const double padding = 12;
                    var maxY = Math.Max(0, Height - btn.Height - padding - 72); // 72 ~ status/header safe offset
                    var clamped = Math.Min(Math.Max(0, proposed), maxY);
                    btn.TranslationY = clamped;
                    break;
                }

                case GestureStatus.Canceled:
                case GestureStatus.Completed:
                default:
                    break;
            }
        }

        private async void GpsButton_OnTapped(object? sender, TappedEventArgs e)
        {
            await SetCurrentLocationAsync();
        }

        private async Task SetCurrentLocationAsync()
        {
            try
            {
                var perm = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (perm != PermissionStatus.Granted)
                {
                    await DisplayAlertAsync("Quyền vị trí", "Bạn cần cấp quyền vị trí để dùng chức năng này.", "OK");
                    return;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request) ?? await Geolocation.GetLastKnownLocationAsync();
                if (location is null)
                {
                    await DisplayAlertAsync("Không lấy được vị trí", "Không thể lấy GPS. Hãy bật Location trên thiết bị/emulator và thử lại.", "OK");
                    return;
                }

                var lat = location.Latitude;
                var lng = location.Longitude;
                var lngS = lng.ToString(CultureInfo.InvariantCulture);
                var latS = lat.ToString(CultureInfo.InvariantCulture);
                var label = $"Vị trí hiện tại ({lat.ToString("F5", CultureInfo.InvariantCulture)}, {lng.ToString("F5", CultureInfo.InvariantCulture)})";

                _suppressTextEvents = true;
                try
                {
                    if (_activeField == ActiveField.Pickup)
                    {
                        _placingPickup = true;
                        ApplySelectionStyle();
                        SetActiveEntryVisibility();
                        _pickupLat = lat;
                        _pickupLng = lng;
                        PickupEntry.Text = label;
                        PickupEntry.Focus();
                        await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngS}, {latS}); flyTo({lngS}, {latS});");
                    }
                    else
                    {
                        _placingPickup = false;
                        ApplySelectionStyle();
                        SetActiveEntryVisibility();
                        _dropLat = lat;
                        _dropLng = lng;
                        DropoffEntry.Text = label;
                        DropoffEntry.Focus();
                        await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngS}, {latS}); flyTo({lngS}, {latS});");
                    }
                }
                finally
                {
                    _suppressTextEvents = false;
                }

                UpdateSummary();
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlertAsync("Không hỗ trợ", "Thiết bị không hỗ trợ định vị.", "OK");
            }
            catch (FeatureNotEnabledException)
            {
                await DisplayAlertAsync("Chưa bật vị trí", "Bạn cần bật Location/GPS để dùng chức năng này.", "OK");
            }
            catch
            {
                await DisplayAlertAsync("Lỗi", "Có lỗi khi lấy vị trí hiện tại. Thử lại giúp mình nhé.", "OK");
            }
        }

        private async Task UpdateSuggestionsAsync(string? query)
        {
            // Gợi ý khi gõ: từ 2 ký tự, danh sách nhiều địa điểm trong VN để khách chọn (giống bấm Tìm).
            _suggestCts?.Cancel();
            _suggestCts = new CancellationTokenSource();
            var ct = _suggestCts.Token;

            query = (query ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                SetSuggestions(Array.Empty<MapboxSuggestion>());
                return;
            }

            try
            {
                // Debounce để giảm số lần gọi API khi người dùng gõ nhanh.
                await Task.Delay(250, ct);
                var items = await GeocodeAsync(query, ct, limit: 10, autocomplete: true);
                SetSuggestions(items);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch
            {
                SetSuggestions(Array.Empty<MapboxSuggestion>());
            }
        }

        private void SetSuggestions(IReadOnlyList<MapboxSuggestion> items)
        {
            SuggestionsList.ItemsSource = items;
            SuggestionsContainer.IsVisible = items.Count > 0;
        }

        private async void SuggestionsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Người dùng chọn 1 gợi ý => đặt marker tương ứng (đón/đến) + bay tới vị trí.
            if (e.CurrentSelection.FirstOrDefault() is not MapboxSuggestion s)
                return;

            SuggestionsList.SelectedItem = null;
            SuggestionsContainer.IsVisible = false;

            await ApplyMapboxSuggestionAsync(s);
        }

        private async Task ApplyMapboxSuggestionAsync(MapboxSuggestion s)
        {
            var lngS = s.Lng.ToString(CultureInfo.InvariantCulture);
            var latS = s.Lat.ToString(CultureInfo.InvariantCulture);

            _suppressTextEvents = true;
            try
            {
                if (_activeField == ActiveField.Pickup)
                {
                    PickupEntry.Text = s.Name;
                    _pickupLat = s.Lat;
                    _pickupLng = s.Lng;
                    await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngS}, {latS}); flyTo({lngS}, {latS});");
                }
                else
                {
                    DropoffEntry.Text = s.Name;
                    _dropLat = s.Lat;
                    _dropLng = s.Lng;
                    await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngS}, {latS}); flyTo({lngS}, {latS});");
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            UpdateSummary();
        }

        private static async Task<IReadOnlyList<MapboxSuggestion>> GeocodeAsync(
            string query,
            CancellationToken ct,
            int limit = 6,
            bool autocomplete = true)
        {
            // Mapbox Forward Geocoding:
            // - autocomplete=true: gợi ý khi gõ; false: chuỗi đủ dài / tìm một địa điểm cụ thể
            // - language=vi; country=vn: chỉ kết quả trong Việt Nam
            var token = Uri.EscapeDataString(MapboxConfig.AccessToken);
            var q = Uri.EscapeDataString(query);
            var auto = autocomplete ? "true" : "false";

            var url =
                $"https://api.mapbox.com/geocoding/v5/mapbox.places/{q}.json" +
                $"?access_token={token}&language=vi&limit={limit}&autocomplete={auto}&country=vn";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await Http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                return Array.Empty<MapboxSuggestion>();

            var list = new List<MapboxSuggestion>();
            foreach (var f in features.EnumerateArray())
            {
                if (!f.TryGetProperty("place_name", out var placeNameEl)) continue;
                var placeName = placeNameEl.GetString();
                if (string.IsNullOrWhiteSpace(placeName)) continue;

                if (!f.TryGetProperty("center", out var centerEl) || centerEl.ValueKind != JsonValueKind.Array) continue;
                if (centerEl.GetArrayLength() < 2) continue;

                var lng = centerEl[0].GetDouble();
                var lat = centerEl[1].GetDouble();

                list.Add(new MapboxSuggestion(placeName, lat, lng));
            }

            return list;
        }

        private async void MapHybrid_OnRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
        {
            // Nhận message từ trang mapbox-home.html (JS) gửi sang C#:
            // - type=ready: WebView sẵn sàng => gọi startMap(token)
            // - type=mapClick: user chạm bản đồ => nhận lat/lng và đặt marker
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(e.Message))
                        return;

                    using var doc = JsonDocument.Parse(e.Message);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();
                    if (type == "ready")
                    {
                        // Mapbox GL cần public token (pk.*). Không có hoặc sai → tiles không tải (màn trắng).
                        if (!MapboxConfig.TryGetAccessToken(out var token) ||
                            !MapboxConfig.LooksLikeMapboxPublicToken(token))
                        {
                            var input = await DisplayPromptAsync(
                                "Cấu hình Mapbox",
                                "Lấy token miễn phí: đăng nhập mapbox.com → Account → Access tokens → Create token (Public scopes mặc định).\n\n" +
                                "Dán public token (bắt đầu bằng pk.) — Android emulator không đọc biến môi trường Windows.",
                                accept: "Lưu",
                                cancel: "Hủy",
                                placeholder: "pk....",
                                maxLength: 220,
                                keyboard: Keyboard.Text);

                            if (string.IsNullOrWhiteSpace(input))
                            {
                                await DisplayAlertAsync(
                                    "Bản đồ chưa hiển thị",
                                    "Cần token Mapbox hợp lệ. Mở lại tab Bản đồ để nhập, hoặc tạo file Resources/Raw/mapbox_token.txt (một dòng pk...).",
                                    "OK");
                                return;
                            }

                            MapboxConfig.SaveAccessToken(input);
                            token = MapboxConfig.AccessToken.Trim();
                            if (!MapboxConfig.LooksLikeMapboxPublicToken(token))
                            {
                                await DisplayAlertAsync("Token không hợp lệ", "Public token phải bắt đầu bằng pk. và đủ dài.", "OK");
                                return;
                            }
                        }

                        var tokenJson = JsonSerializer.Serialize(token);
                        await MapHybrid.EvaluateJavaScriptAsync($"startMap({tokenJson});");
                        await Task.Delay(350);
                        try
                        {
                            await MapHybrid.EvaluateJavaScriptAsync(
                                "if(typeof window.__mapboxResizeMap==='function')window.__mapboxResizeMap();");
                        }
                        catch
                        {
                            /* ignore */
                        }

                        return;
                    }

                    if (type == "mapError")
                    {
                        var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Lỗi Mapbox không xác định.";
                        await DisplayAlertAsync(
                            "Bản đồ không tải được",
                            $"{msg}\n\n" +
                            "Kiểm tra Mapbox: Account → token của bạn — **bỏ giới hạn URL** (URL restrictions) cho bản dev, hoặc tạo token mới không hạn chế.\n" +
                            "Emulator cần Internet. Sau đó đóng app và mở lại tab Bản đồ.",
                            "OK");
                        return;
                    }

                    if (type == "mapClick")
                    {
                        // Click trên map: đặt điểm đón/đến tùy theo trạng thái đang chọn.
                        var lat = root.GetProperty("lat").GetDouble();
                        var lng = root.GetProperty("lng").GetDouble();
                        var lngS = lng.ToString(CultureInfo.InvariantCulture);
                        var latS = lat.ToString(CultureInfo.InvariantCulture);
                        if (_placingPickup)
                        {
                            _pickupLat = lat;
                            _pickupLng = lng;
                            await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngS}, {latS});");
                        }
                        else
                        {
                            _dropLat = lat;
                            _dropLng = lng;
                            await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngS}, {latS});");
                        }

                        UpdateSummary();
                    }
                }
                catch
                {
                    // ignore malformed messages
                }
            });
        }

        private void UpdateSummary()
        {
            // Tóm tắt nhanh tọa độ đã chọn để người dùng biết đã set điểm nào.
            var parts = new List<string>();
            if (_pickupLat is double plat && _pickupLng is double plng)
                parts.Add($"Đón: {plat.ToString("F5", CultureInfo.InvariantCulture)}, {plng.ToString("F5", CultureInfo.InvariantCulture)}");
            if (_dropLat is double dlat && _dropLng is double dlng)
                parts.Add($"Đến: {dlat.ToString("F5", CultureInfo.InvariantCulture)}, {dlng.ToString("F5", CultureInfo.InvariantCulture)}");

            SummaryLabel.Text = parts.Count > 0 ? string.Join(" · ", parts) : "Chưa chọn điểm.";
        }

        private async void DemoDbButton_OnClicked(object? sender, EventArgs e)
        {
            await ShowDistributedDatabaseStatsAsync();
        }

        private async Task ShowDistributedDatabaseStatsAsync()
        {
            try
            {
                // Get database stats
                var stats = await DistributedDatabaseService.GetDatabaseStatsAsync();

                // Build message
                var message = "Distributed Database Stats:\n\n";
                foreach (var stat in stats)
                {
                    message += $"{stat.DatabaseName}: {stat.RecordCount:N0} records\n";
                }
                message += $"\nUpdated: {DateTime.Now:HH:mm:ss}";

                // Save stats for future reference
                await DistributedDatabaseService.SaveStatsAsync(stats);

                // Show alert
                await DisplayAlert(
                    "Database Statistics",
                    message,
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Error",
                    $"Failed to fetch database stats: {ex.Message}",
                    "OK");
            }
        }
    }
}
