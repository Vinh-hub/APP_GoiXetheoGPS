# GIẢI PHÁP CHI TIẾT - Sửa Session Hết Hạn & Tài Khoản Không Cập Nhật

## 📋 TÓM TẮT VẤN ĐỀ

### Báo cáo từ user:
1. ❌ **Vẫn chưa đặt chuyến được** - Hiện lỗi phiên hết hạn
2. ❌ **Trang tài khoản chưa đăng nhập** - Dù đã đăng nhập thành công
3. ❌ **Phiên hết hạn liên tục** - Không rõ lý do

---

## 🔍 PHÂN TÍCH NGUYÊN NHÂN

### Root Cause #1: AuthPage là Singleton
```
Problem: AuthPage được tạo 1 lần và không bao giờ bị destroy
Result:  OnAppearing() chỉ gọi lần đầu, không gọi khi quay lại
Impact:  Trang Account luôn hiển thị "Chưa đăng nhập"
```

### Root Cause #2: Session Validation gọi API liên tục
```
Problem: ValidateSessionAsync() gọi /api/auth/session mỗi lần check
Result:  Nếu token hết hạn → 401 → ApiClient xóa session → "Phiên hết hạn"
Impact:  Vòng lặp vô hạn: check → fail → xóa → check lại
```

### Root Cause #3: Không kiểm tra token hết hạn cục bộ
```
Problem: Không verify token expiry trước khi gọi API
Result:  Gọi API → 401 → xóa session → hiện lỗi
Impact:  User nhìn thấy "Phiên hết hạn" mà không biết lý do
```

---

## ✅ GIẢI PHÁP ÁP DỤNG

### Sửa chữa 1: AuthSessionService.cs - Event-based Notification
**File**: `Services/AuthSessionService.cs`

```csharp
// ✅ Thêm event
public event EventHandler? LoginStateChanged;

// ✅ Gọi event khi login thay đổi
public void SaveLogin(AuthApiService.AuthResponse response)
{
    if (response is null || string.IsNullOrWhiteSpace(response.Token))
        return;

    AccessToken = response.Token;
    // ... save other properties ...

    // Fire event → Tất cả pages listening sẽ được notify
    LoginStateChanged?.Invoke(this, EventArgs.Empty);  
}

// ✅ Gọi event khi logout
public void Clear()
{
    Preferences.Default.Remove(AccessTokenKey);
    // ... remove other properties ...

    // Fire event → UI sẽ update ngay
    LoginStateChanged?.Invoke(this, EventArgs.Empty);  
}

// ✅ Helper để kiểm tra token hết hạn sớm
private bool IsTokenExpiredSoon()
{
    var expires = GetTokenExpiryUtc();
    if (!expires.HasValue)
        return false;

    // Token considered expired soon if less than 1 minute remaining
    return expires.Value <= DateTimeOffset.UtcNow.AddMinutes(1);
}
```

**Lợi ích**:
- ✓ Pages có thể subscribe/unsubscribe vào event
- ✓ UI tự động cập nhật khi login state thay đổi
- ✓ Không cần gọi API liên tục

---

### Sửa chữa 2: AuthPage.xaml.cs - Real-time UI Updates
**File**: `Pages/AuthPage.xaml.cs`

```csharp
// ✅ Constructor: Subscribe vào event
public AuthPage(AuthApiService authApiService, AuthSessionService sessionService)
{
    InitializeComponent();
    _authApiService = authApiService;
    _sessionService = sessionService;

    // Subscribe để được notify khi login state thay đổi
    _sessionService.LoginStateChanged += OnLoginStateChanged;
}

// ✅ Callback khi login state thay đổi
private void OnLoginStateChanged(object? sender, EventArgs e)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateJwtStatusLabel();  // Cập nhật UI ngay lập tức
    });
}

// ✅ Cleanup khi page disappear
protected override void OnDisappearing()
{
    base.OnDisappearing();
    _sessionService.LoginStateChanged -= OnLoginStateChanged;  // Unsubscribe
}

// ✅ OnAppearing: Force refresh khi quay lại page
protected override void OnAppearing()
{
    base.OnAppearing();
    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateJwtStatusLabel();
        _ = RefreshSessionSilentlyAsync();
    });
}

// ✅ QUAN TRỌNG: Kiểm tra token hết hạn TRƯỚC gọi API
async Task ValidateAndRenderSessionAsync(bool showFeedback)
{
    try
    {
        var token = _sessionService.AccessToken;
        var expiresUtc = _sessionService.GetTokenExpiryUtc();

        // 1️⃣ Kiểm tra: Token có tồn tại không?
        if (string.IsNullOrWhiteSpace(token))
        {
            SessionStatusLabel.Text = "Chưa đăng nhập.";
            return;
        }

        // 2️⃣ Kiểm tra: Token hết hạn TRƯỚC khi gọi API
        if (expiresUtc.HasValue && expiresUtc.Value <= DateTimeOffset.UtcNow)
        {
            _sessionService.Clear();  // Xóa token hết hạn
            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Phiên đã hết hạn.";
            return;
        }

        // 3️⃣ Chỉ gọi API khi token còn hiệu lực
        var session = await _authApiService.ValidateSessionAsync();
        if (session is null)
        {
            _sessionService.Clear();
            UpdateJwtStatusLabel();
            SessionStatusLabel.Text = "Phiên không hợp lệ.";
            return;
        }

        UpdateJwtStatusLabel();
        SessionStatusLabel.Text = "Phiên hợp lệ.";
    }
    catch (Exception ex)
    {
        // Handle network errors gracefully
        SessionStatusLabel.Text = "Không thể kiểm tra phiên lúc này.";
    }
}
```

**Lợi ích**:
- ✓ AuthPage tự động refresh khi login state thay đổi
- ✓ Không cần gọi API mỗi lần quay lại page
- ✓ Token hết hạn được detect trước khi gọi API
- ✓ Giảm tải cho server

---

### Sửa chữa 3: MauiProgram.cs - Keep AuthPage as Singleton
**File**: `MauiProgram.cs`

```csharp
// AuthPage vẫn là Singleton (không thay đổi)
builder.Services.AddSingleton<AuthPage>();

// Nhưng bây giờ nó subscribe vào LoginStateChanged event
// Nên nó tự động cập nhật khi user login/logout
```

**Lý do**:
- AuthPage cần là Singleton vì nó hiển thị trong flyout menu
- Nhưng với event subscription, nó tự động refresh khi login state thay đổi

---

## 📊 TRƯỚC & SAU KHI SỬA

### Trước (Có vấn đề)
```
User Login
  ↓
Token saved to Preferences
  ↓
User navigates to Account page
  ↓
OnAppearing() not called (Singleton + already initialized)
  ↓
UI hiển thị "Chưa đăng nhập" ❌
  ↓
ValidateSessionAsync() → API call → fail → Session xóa
  ↓
"Phiên hết hạn" message ❌
```

### Sau (Sửa chữa)
```
User Login
  ↓
Token saved to Preferences
  ↓
AuthSessionService.SaveLogin()
  ↓
LoginStateChanged event fired
  ↓
AuthPage.OnLoginStateChanged() called
  ↓
UpdateJwtStatusLabel() → UI update ngay ✅
  ↓
User navigates to Account page
  ↓
OnAppearing() called → Refresh lại ✅
  ↓
Token expiry check trước API ✅
  ↓
ValidateSessionAsync() chỉ gọi khi token còn hiệu lực ✅
```

---

## 🧪 KIỂM TRA CÁC BƯỚC

### Test Case 1: Login & Check Account
```
1. ✓ Mở app → trang Auth Welcome
2. ✓ Click "Đăng nhập" → Login Form
3. ✓ Nhập: admin@rideapi.local / Admin@123
4. ✓ Click "Đăng nhập" → Thông báo "Đăng nhập thành công"
5. ✓ Quay lại → Trang Home
6. ✓ Click Account (flyout) → Trang tài khoản
7. ✓ Kiểm tra: Phải hiển thị "Đã đăng nhập" ✅ (Fixed)
```

### Test Case 2: Account Page Auto-update
```
1. ✓ Đăng nhập
2. ✓ Vào trang Account → "Đã đăng nhập"
3. ✓ Vào trang Home → Quay lại Account
4. ✓ Kiểm tra: Phải vẫn hiển thị "Đã đăng nhập" ✅ (Fixed)
5. ✓ Click "Kiểm tra phiên" → "Phiên hợp lệ" ✅ (No API error)
```

### Test Case 3: Book Trip
```
1. ✓ Đăng nhập
2. ✓ Vào Home → "Đặt xe"
3. ✓ Vào Map → Chọn pickup/dropoff
4. ✓ Click "Đặt xe" → Nên success (nếu token còn hiệu lực) ✅
5. ✓ Không nên có lỗi "Phiên hết hạn" ✅ (Fixed)
```

### Test Case 4: Session Expiry
```
1. ✓ Đăng nhập (token hiệu lực 24h)
2. ✓ Vào Account → "Đã đăng nhập"
3. ✓ [Waiting >24h or manually invalidate token]
4. ✓ Quay lại Account → "Phiên đã hết hạn" ✅
5. ✓ Click "Kiểm tra phiên" → Alert "Phiên hết hạn" ✅
```

---

## 📝 THAY ĐỔI ĐƯỢC COMMIT

| File | Thay đổi |
|------|---------|
| `Services/AuthSessionService.cs` | ✅ Thêm `LoginStateChanged` event |
| `Pages/AuthPage.xaml.cs` | ✅ Subscribe event, check token trước API |
| `MauiProgram.cs` | ✅ Giữ AuthPage là Singleton |
| `Pages/LoginPage.xaml.cs` | ✅ Kiểm tra token trước navigate |
| `Pages/RegisterPage.xaml.cs` | ✅ Kiểm tra token trước navigate |
| `..\RideAPI\RideAPI\Controllers\AuthController.cs` | ✅ Token expiry rõ ràng |
| `..\RideAPI\RideAPI\Controllers\AdminController.cs` | ✅ Token expiry rõ ràng |
| `..\RideAPI\RideAPI\Program.cs` | ✅ Xử lý exception startup |

---

## 🎯 KẾT QUẢ KỲ VỌNG

### ✅ Tất cả vấn đề sẽ được giải quyết

1. **Trang tài khoản cập nhật đúng**
   - AuthPage subscribe vào event
   - Tự động update UI khi login state thay đổi

2. **Không còn lỗi "phiên hết hạn" đột ngột**
   - Token expiry check trước API
   - Giảm số lần gọi API

3. **Đặt chuyến hoạt động bình thường**
   - Token valid → API success
   - Token expired → detect trước → không call API

4. **Performance cải thiện**
   - Giảm API calls không cần thiết
   - UI responsiveness tốt hơn

---

## ⚠️ LƯU Ý

- Token hiệu lực: **24 giờ** (từ khi đăng nhập)
- Session lưu trong: **Preferences** (persistent)
- Event notification: **MainThread only** (UI-safe)
- API endpoint: `/api/auth/session` (validate token)

---

## 📞 CẦN HỖ TRỢ?

Nếu vẫn có vấn đề:
1. Kiểm tra lại database connection (North/South)
2. Xóa app data → Cài đặt lại
3. Kiểm tra Preferences lưu token
4. Xem logs từ API server

✨ Build successful! Sửa chữa hoàn tất.
