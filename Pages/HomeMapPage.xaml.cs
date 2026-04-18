using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using APP_GoiXetheoGPS.Configuration;
using APP_GoiXetheoGPS.Models;
using APP_GoiXetheoGPS.Services;

namespace APP_GoiXetheoGPS.Pages;

public partial class HomeMapPage : ContentPage
{
    private double _gpsButtonStartY;
    private static readonly HttpClient Http = new();

    private readonly AppSettingsService _settingsService;
    private readonly RideApiService _rideApiService;
    private readonly ObservableCollection<NearbyDriverItem> _nearbyDrivers = new();

    private bool _placingPickup = true;
    private double? _pickupLat;
    private double? _pickupLng;
    private double? _dropLat;
    private double? _dropLng;
    private CancellationTokenSource? _suggestCts;
    private bool _suppressTextEvents;
    private NearbyDriverItem? _selectedDriver;

    private enum ActiveField { Pickup, Dropoff }
    private ActiveField _activeField = ActiveField.Pickup;

    private sealed record MapboxSuggestion(string Name, double Lat, double Lng);

    public HomeMapPage()
    {
        InitializeComponent();

        _settingsService = ServiceHelper.GetService<AppSettingsService>() ?? new AppSettingsService();
        _rideApiService = ServiceHelper.GetService<RideApiService>() ?? new RideApiService(_settingsService);

        SuggestionsList.ItemsSource = Array.Empty<MapboxSuggestion>();
        NearbyDriversList.ItemsSource = _nearbyDrivers;
        RegionPicker.ItemsSource = _settingsService.GetRegionOptions();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplySelectionStyle();
        SetActiveEntryVisibility();
        SyncSettingsUi();
        UpdateSummary();
        UpdateSelectedDriverUi();
        UpdateEstimatedPrice();
    }

    private void SyncSettingsUi()
    {
        var regions = _settingsService.GetRegionOptions();
        var selectedRegion = _settingsService.GetSelectedRegion();
        RegionPicker.SelectedItem = regions.FirstOrDefault(x => x.Key == selectedRegion.Key) ?? regions.FirstOrDefault();
        SettingsSummaryLabel.Text = _settingsService.BuildConfigurationSummary();
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

    private void ApplySelectionStyle()
    {
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
        if (_suppressTextEvents)
            return;

        _activeField = ActiveField.Pickup;
        await UpdateSuggestionsAsync(e.NewTextValue);
    }

    private async void DropoffEntry_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents)
            return;

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

    private async void ConfigureApiButton_OnClicked(object? sender, EventArgs e)
    {
        var baseUrl = await DisplayPromptAsync(
            "Cấu hình API",
            "Nhập Base URL backend, ví dụ https://localhost:5001 hoặc http://192.168.1.10:5000",
            accept: "Lưu",
            cancel: "Hủy",
            placeholder: "https://localhost:5001",
            initialValue: _settingsService.GetApiBaseUrl(),
            keyboard: Keyboard.Url);

        if (baseUrl is null)
            return;

        var token = await DisplayPromptAsync(
            "JWT người dùng",
            "Dán JWT đã copy từ Swagger hoặc backend login. Có thể để trống nếu chỉ cần nearby drivers.",
            accept: "Lưu",
            cancel: "Hủy",
            placeholder: "eyJhbGciOi...",
            initialValue: _settingsService.GetJwtToken(),
            keyboard: Keyboard.Text);

        if (token is null)
            return;

        _settingsService.SaveApiBaseUrl(baseUrl);
        _settingsService.SaveJwtToken(token);
        SyncSettingsUi();
        await DisplayAlertAsync("Đã lưu", "Đã cập nhật cấu hình API cho phần demo app.", "OK");
    }

    private void RegionPicker_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (RegionPicker.SelectedItem is not RegionOption region)
            return;

        _settingsService.SaveSelectedRegion(region.Key);
        SyncSettingsUi();
        ClearNearbyDrivers();
    }

    private async void LoadNearbyDriversButton_OnClicked(object? sender, EventArgs e)
    {
        await LoadNearbyDriversAsync();
    }

    private async Task LoadNearbyDriversAsync()
    {
        if (!_settingsService.HasApiBaseUrlConfigured())
        {
            await DisplayAlertAsync("Thiếu cấu hình", "Bạn cần cấu hình Base URL trước khi tải tài xế gần đây.", "OK");
            return;
        }

        if (!_pickupLat.HasValue || !_pickupLng.HasValue)
        {
            await DisplayAlertAsync("Thiếu điểm đón", "Hãy chọn điểm đón trước khi tải tài xế gần đây.", "OK");
            return;
        }

        try
        {
            BookRideButton.IsEnabled = false;
            HideReadOnlyBanner();

            var drivers = await _rideApiService.GetNearbyDriversAsync(_pickupLat.Value, _pickupLng.Value);

            _nearbyDrivers.Clear();
            foreach (var driver in drivers)
                _nearbyDrivers.Add(driver);

            _selectedDriver = null;
            NearbyDriversList.SelectedItem = null;
            UpdateSelectedDriverUi();

            if (_nearbyDrivers.Count == 0)
                await DisplayAlertAsync("Không có tài xế", "Không tìm thấy tài xế nào gần điểm đón hiện tại.", "OK");
        }
        catch (RideApiException ex)
        {
            if (ex.IsReadOnly)
                ShowReadOnlyBanner(ex.Message);

            await DisplayAlertAsync("Tải tài xế thất bại", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Tải tài xế thất bại", ex.Message, "OK");
        }
        finally
        {
            BookRideButton.IsEnabled = true;
        }
    }

    private void NearbyDriversList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedDriver = e.CurrentSelection.FirstOrDefault() as NearbyDriverItem;
        UpdateSelectedDriverUi();
    }

    private void UpdateSelectedDriverUi()
    {
        SelectedDriverLabel.Text = _selectedDriver is null
            ? "Chưa chọn tài xế."
            : $"Đã chọn: {_selectedDriver.Name} • {_selectedDriver.DistanceDisplay} • {_selectedDriver.StatusDisplay}";
    }

    private async void BookRideButton_OnClicked(object? sender, EventArgs e)
    {
        var validationMessage = ValidateBookRide();
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            await DisplayAlertAsync("Chưa thể đặt xe", validationMessage, "OK");
            return;
        }

        try
        {
            BookRideButton.IsEnabled = false;
            HideReadOnlyBanner();

            var response = await _rideApiService.BookRideAsync(new CreateRideRequest
            {
                DriverId = _selectedDriver!.DriverId,
                Price = EstimatePrice(),
                StartLat = _pickupLat!.Value,
                StartLng = _pickupLng!.Value,
                EndLat = _dropLat!.Value,
                EndLng = _dropLng!.Value
            });

            await DisplayAlertAsync("Đặt xe thành công", $"{response.Message}. Mã chuyến: {response.TripId}", "OK");
            await Shell.Current.GoToAsync("//trips");
        }
        catch (RideApiException ex)
        {
            if (ex.IsReadOnly)
            {
                const string readOnlyMessage = "Hệ thống đang ở chế độ chỉ đọc, không thể đặt chuyến mới.";
                ShowReadOnlyBanner(readOnlyMessage);
                await DisplayAlertAsync("Chế độ chỉ đọc", readOnlyMessage, "OK");
                return;
            }

            await DisplayAlertAsync("Đặt xe thất bại", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Đặt xe thất bại", ex.Message, "OK");
        }
        finally
        {
            BookRideButton.IsEnabled = true;
        }
    }

    private string? ValidateBookRide()
    {
        if (!_settingsService.HasApiBaseUrlConfigured())
            return "Thiếu cấu hình API base URL.";

        if (!_settingsService.HasJwtConfigured())
            return "Thiếu JWT của user để gọi API đặt chuyến.";

        if (!_pickupLat.HasValue || !_pickupLng.HasValue)
            return "Thiếu điểm đón.";

        if (!_dropLat.HasValue || !_dropLng.HasValue)
            return "Thiếu điểm đến.";

        if (_selectedDriver is null)
            return "Bạn chưa chọn tài xế.";

        return null;
    }

    private void ShowReadOnlyBanner(string message)
    {
        ReadOnlyBannerLabel.Text = message;
        ReadOnlyBanner.IsVisible = true;
    }

    private void HideReadOnlyBanner()
    {
        ReadOnlyBanner.IsVisible = false;
        ReadOnlyBannerLabel.Text = string.Empty;
    }

    private void ClearNearbyDrivers()
    {
        _nearbyDrivers.Clear();
        _selectedDriver = null;
        NearbyDriversList.SelectedItem = null;
        UpdateSelectedDriverUi();
    }

    private decimal EstimatePrice()
    {
        if (!_pickupLat.HasValue || !_pickupLng.HasValue || !_dropLat.HasValue || !_dropLng.HasValue)
            return 0;

        var distanceKm = CalculateDistanceKm(_pickupLat.Value, _pickupLng.Value, _dropLat.Value, _dropLng.Value);
        var price = 15000m + (decimal)distanceKm * 9000m;
        return Math.Round(price, 0, MidpointRounding.AwayFromZero);
    }

    private void UpdateEstimatedPrice()
    {
        var price = EstimatePrice();
        EstimatedPriceLabel.Text = price <= 0
            ? "Giá ước tính: chưa có"
            : $"Giá ước tính: {price:N0} đ";
    }

    private static double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private async Task SearchNamedPlaceForActiveFieldAsync(string? query)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            await DisplayAlertAsync("Tìm kiếm", "Nhập tên địa điểm với ít nhất 2 ký tự.", "OK");
            return;
        }

        if (!MapboxConfig.TryGetAccessToken(out _) ||
            string.Equals(MapboxConfig.AccessToken, "YOUR_MAPBOX_PUBLIC_TOKEN", StringComparison.Ordinal))
        {
            await DisplayAlertAsync("Thiếu Mapbox token", "Cần cấu hình token Mapbox (pk.) để tìm địa điểm theo tên.", "OK");
            return;
        }

        try
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var items = await GeocodeAsync(query, cancellationSource.Token, limit: 10, autocomplete: true);
            if (items.Count == 0)
            {
                await DisplayAlertAsync("Không tìm thấy", $"Không có kết quả cho \"{query}\".", "OK");
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
            await DisplayAlertAsync("Tìm kiếm", "Hết thời gian chờ. Hãy thử lại.", "OK");
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync("Tìm kiếm", "Không gọi được Mapbox. Kiểm tra mạng hoặc token.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Tìm kiếm", "Có lỗi khi tìm địa điểm. Hãy thử lại.", "OK");
        }
    }

    private void GpsButton_OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (sender is not VisualElement button)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _gpsButtonStartY = button.TranslationY;
                break;
            case GestureStatus.Running:
                var proposed = _gpsButtonStartY + e.TotalY;
                const double padding = 12;
                var maxY = Math.Max(0, Height - button.Height - padding - 72);
                button.TranslationY = Math.Min(Math.Max(0, proposed), maxY);
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
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlertAsync("Quyền vị trí", "Bạn cần cấp quyền vị trí để dùng chức năng này.", "OK");
                return;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.GetLocationAsync(request) ?? await Geolocation.GetLastKnownLocationAsync();
            if (location is null)
            {
                await DisplayAlertAsync("Không lấy được vị trí", "Không thể lấy GPS. Hãy kiểm tra Location trên thiết bị.", "OK");
                return;
            }

            var lat = location.Latitude;
            var lng = location.Longitude;
            var lngText = lng.ToString(CultureInfo.InvariantCulture);
            var latText = lat.ToString(CultureInfo.InvariantCulture);
            var label = $"Vị trí hiện tại ({lat:F5}, {lng:F5})";

            _suppressTextEvents = true;
            try
            {
                if (_activeField == ActiveField.Pickup)
                {
                    _placingPickup = true;
                    _pickupLat = lat;
                    _pickupLng = lng;
                    PickupEntry.Text = label;
                    await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngText}, {latText}); flyTo({lngText}, {latText});");
                    ClearNearbyDrivers();
                }
                else
                {
                    _placingPickup = false;
                    _dropLat = lat;
                    _dropLng = lng;
                    DropoffEntry.Text = label;
                    await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngText}, {latText}); flyTo({lngText}, {latText});");
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
            await DisplayAlertAsync("Chưa bật vị trí", "Bạn cần bật GPS/Location để dùng chức năng này.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Lỗi", "Có lỗi khi lấy vị trí hiện tại. Hãy thử lại.", "OK");
        }
    }

    private async Task UpdateSuggestionsAsync(string? query)
    {
        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var cancellationToken = _suggestCts.Token;

        query = (query ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            SetSuggestions(Array.Empty<MapboxSuggestion>());
            return;
        }

        try
        {
            await Task.Delay(250, cancellationToken);
            var items = await GeocodeAsync(query, cancellationToken, limit: 10, autocomplete: true);
            SetSuggestions(items);
        }
        catch (OperationCanceledException)
        {
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
        if (e.CurrentSelection.FirstOrDefault() is not MapboxSuggestion suggestion)
            return;

        SuggestionsList.SelectedItem = null;
        SuggestionsContainer.IsVisible = false;
        await ApplyMapboxSuggestionAsync(suggestion);
    }

    private async Task ApplyMapboxSuggestionAsync(MapboxSuggestion suggestion)
    {
        var lngText = suggestion.Lng.ToString(CultureInfo.InvariantCulture);
        var latText = suggestion.Lat.ToString(CultureInfo.InvariantCulture);

        _suppressTextEvents = true;
        try
        {
            if (_activeField == ActiveField.Pickup)
            {
                PickupEntry.Text = suggestion.Name;
                _pickupLat = suggestion.Lat;
                _pickupLng = suggestion.Lng;
                await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngText}, {latText}); flyTo({lngText}, {latText});");
                ClearNearbyDrivers();
            }
            else
            {
                DropoffEntry.Text = suggestion.Name;
                _dropLat = suggestion.Lat;
                _dropLng = suggestion.Lng;
                await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngText}, {latText}); flyTo({lngText}, {latText});");
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
        CancellationToken cancellationToken,
        int limit = 6,
        bool autocomplete = true)
    {
        var token = Uri.EscapeDataString(MapboxConfig.AccessToken);
        var encodedQuery = Uri.EscapeDataString(query);
        var auto = autocomplete ? "true" : "false";

        var url =
            $"https://api.mapbox.com/geocoding/v5/mapbox.places/{encodedQuery}.json" +
            $"?access_token={token}&language=vi&limit={limit}&autocomplete={auto}&country=vn";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            return Array.Empty<MapboxSuggestion>();

        var items = new List<MapboxSuggestion>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("place_name", out var placeNameElement))
                continue;

            var placeName = placeNameElement.GetString();
            if (string.IsNullOrWhiteSpace(placeName))
                continue;

            if (!feature.TryGetProperty("center", out var centerElement) || centerElement.ValueKind != JsonValueKind.Array || centerElement.GetArrayLength() < 2)
                continue;

            items.Add(new MapboxSuggestion(placeName, centerElement[1].GetDouble(), centerElement[0].GetDouble()));
        }

        return items;
    }

    private async void MapHybrid_OnRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(e.Message))
                    return;

                using var document = JsonDocument.Parse(e.Message);
                var root = document.RootElement;
                var type = root.GetProperty("type").GetString();

                if (type == "ready")
                {
                    if (!MapboxConfig.TryGetAccessToken(out var token))
                    {
                        var input = await DisplayPromptAsync(
                            "Thiếu cấu hình Mapbox",
                            "Dán Mapbox public token (bắt đầu bằng pk.) để lưu trên máy này.",
                            accept: "Lưu",
                            cancel: "Hủy",
                            placeholder: "pk....",
                            maxLength: 200,
                            keyboard: Keyboard.Text);

                        if (string.IsNullOrWhiteSpace(input))
                            return;

                        MapboxConfig.SaveAccessToken(input);
                        token = MapboxConfig.AccessToken;
                    }

                    var tokenJson = JsonSerializer.Serialize(token);
                    await MapHybrid.EvaluateJavaScriptAsync($"startMap({tokenJson});");
                    return;
                }

                if (type != "mapClick")
                    return;

                var lat = root.GetProperty("lat").GetDouble();
                var lng = root.GetProperty("lng").GetDouble();
                var lngText = lng.ToString(CultureInfo.InvariantCulture);
                var latText = lat.ToString(CultureInfo.InvariantCulture);

                if (_placingPickup)
                {
                    _pickupLat = lat;
                    _pickupLng = lng;
                    await MapHybrid.EvaluateJavaScriptAsync($"setPickupMarker({lngText}, {latText});");
                    ClearNearbyDrivers();
                }
                else
                {
                    _dropLat = lat;
                    _dropLng = lng;
                    await MapHybrid.EvaluateJavaScriptAsync($"setDropoffMarker({lngText}, {latText});");
                }

                UpdateSummary();
            }
            catch
            {
            }
        });
    }

    private void UpdateSummary()
    {
        var parts = new List<string>();
        if (_pickupLat is double pickupLat && _pickupLng is double pickupLng)
            parts.Add($"Đón: {pickupLat:F5}, {pickupLng:F5}");

        if (_dropLat is double dropLat && _dropLng is double dropLng)
            parts.Add($"Đến: {dropLat:F5}, {dropLng:F5}");

        SummaryLabel.Text = parts.Count == 0 ? "Chưa chọn điểm." : string.Join(" • ", parts);
        UpdateEstimatedPrice();
    }
}
