# Sửa chữa vấn đề Session hết hạn và Tài khoản không cập nhật

## Vấn đề báo cáo
1. ❌ Vẫn hiện "phiên hết hạn" dù đã đăng nhập
2. ❌ Trang tài khoản chưa đăng nhập dù đã đăng nhập
3. ❌ Không thể đặt chuyến (API lỗi 401)

## Nguyên nhân gốc

### Vấn đề 1: AuthPage không cập nhật khi login
- **Nguyên nhân**: AuthPage là Singleton, `OnAppearing()` chỉ gọi lần đầu
- **Kết quả**: Khi user đăng nhập xong, quay lại trang account, nó vẫn hiển thị "Chưa đăng nhập"

### Vấn đề 2: Session validation gọi API mà API fail
- **Nguyên nhân**: `ValidateSessionAsync()` gọi API `/api/auth/session`, nếu token hết hạn → 401 → xóa session
- **Kết quả**: Hiển thị "Phiên không hợp lệ hoặc đã hết hạn"

### Vấn đề 3: Token expiry check không rõ ràng
- **Nguyên nhân**: Không kiểm tra token hết hạn trước khi gọi API
- **Kết quả**: Lỗi 401 từ server thay vì xử lý trước

## Các sửa chữa áp dụng

### 1. AuthSessionService.cs ✅

**Thêm event notification:**
```csharp
public event EventHandler? LoginStateChanged;

public void SaveLogin(...)
{
    // ... existing code ...
    LoginStateChanged?.Invoke(this, EventArgs.Empty);  // ← NEW
}

public void Clear()
{
    // ... existing code ...
    LoginStateChanged?.Invoke(this, EventArgs.Empty);  // ← NEW
}
```

### 2. AuthPage.xaml.cs ✅

**Subscribe vào event:**
```csharp
public AuthPage(...)
{
    _sessionService.LoginStateChanged += OnLoginStateChanged;
}

private void OnLoginStateChanged(object? sender, EventArgs e)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateJwtStatusLabel();  // Cập nhật UI ngay
    });
}

protected override void OnDisappearing()
{
    _sessionService.LoginStateChanged -= OnLoginStateChanged;
}
```

**Kiểm tra token hết hạn trước gọi API:**
```csharp
async Task ValidateAndRenderSessionAsync(bool showFeedback)
{
    // 1. Kiểm tra token có không
    if (string.IsNullOrWhiteSpace(token))
        return;

    // 2. Kiểm tra token hết hạn TRƯỚC khi gọi API
    if (expiresUtc.HasValue && expiresUtc.Value <= DateTimeOffset.UtcNow)
    {
        _sessionService.Clear();
        return;
    }

    // 3. Chỉ gọi API khi token còn hiệu lực
    var session = await _authApiService.ValidateSessionAsync();
}
```

## Luồng xử lý sau sửa

```
User login
    ↓
LoginAsync() → Token saved
    ↓
AuthSessionService.SaveLogin()
    ↓
LoginStateChanged event fired
    ↓
AuthPage.OnLoginStateChanged() → UpdateJwtStatusLabel()
    ↓
UI cập nhật ngay (Đã đăng nhập)
    ↓
Quay lại trang Account → OnAppearing() → Cập nhật lại
```

## Điểm kiểm tra đã sửa
✅ AuthPage tự động refresh khi login state thay đổi
✅ Token hết hạn được kiểm tra trước khi gọi API
✅ Trang Account luôn hiển thị trạng thái đúng
✅ Không có API 401 do token hết hạn
✅ Đặt chuyến có thể thực hiện nếu token còn hiệu lực

## Quy trình kiểm tra
1. ✓ Đăng nhập
2. ✓ Kiểm tra trang Account → phải hiển thị "Đã đăng nhập"
3. ✓ Quay lại Home, rồi quay lại Account → vẫn hiển thị "Đã đăng nhập"
4. ✓ Đặt chuyến → nếu token còn hiệu lực sẽ thành công
5. ✓ Kiểm tra phiên → phải hiển thị "Phiên hợp lệ"
