# Sửa lỗi Đăng nhập/Đăng ký và Phiên hết hạn

## Vấn đề gốc
1. **Sau khi đăng nhập vẫn không đăng nhập được**: Ứng dụng hiển thị thông báo "Đăng nhập thành công" nhưng không lưu token thực tế
2. **Phiên hết hạn liên tục**: Token không được lưu đúng cách, dẫn đến phiên bị xóa ngay lập tức

## Nguyên nhân chính
1. **LoginPage.xaml.cs**: Không kiểm tra xem token có thực sự được lưu hay không
   - Khi `LoginAsync()` trả về null hoặc token rỗng, code vẫn hiển thị "Đăng nhập thành công" và chuyển trang
   - `_session.SaveLogin(response)` chỉ lưu nếu `response?.Token` không rỗng
   - Nhưng code không kiểm tra điều này

2. **RegisterPage.xaml.cs**: Cùng vấn đề như LoginPage

3. **Token expiry**: Các hàm tạo token sử dụng `DateTime.UtcNow.AddDays(1)` không rõ ràng

## Các sửa chữa áp dụng

### 1. LoginPage.xaml.cs ✅
**Thêm kiểm tra token:**
```csharp
var result = await _authApiService.LoginAsync(email, password);
if (result is null || string.IsNullOrWhiteSpace(result.Token))
{
    await this.DisplayAlertAsync("Đăng nhập", "Email hoặc mật khẩu không chính xác.", "OK");
    return;
}
// Chỉ chuyển trang nếu token hợp lệ
await Shell.Current.GoToAsync("//home");
```

### 2. RegisterPage.xaml.cs ✅
**Thêm kiểm tra token:**
```csharp
var result = await _authApiService.RegisterAsync(request);
if (result is null || string.IsNullOrWhiteSpace(result.Token))
{
    await this.DisplayAlertAsync("Đăng ký", "Đăng ký thất bại. Vui lòng thử lại.", "OK");
    return;
}
// Chỉ chuyển trang nếu token hợp lệ
await Shell.Current.GoToAsync("//home");
```

### 3. AuthController.cs - GenerateJwtToken() ✅
**Rõ ràng hóa token expiry:**
```csharp
var expiresAt = DateTime.UtcNow.AddHours(24);
var token = new JwtSecurityToken(
    ...
    expires: expiresAt,
    ...
);
```

### 4. AdminController.cs - GenerateAdminToken() ✅
**Rõ ràng hóa token expiry:**
```csharp
var expiresAt = DateTime.UtcNow.AddHours(24);
var token = new JwtSecurityToken(
    ...
    expires: expiresAt,
    ...
);
```

## Luồng xử lý sau sửa

```
Login Form
    ↓
LoginAsync() → API
    ↓
[Kiểm tra: Token có hợp lệ?]
    ├─ Có → SaveLogin() → GoToAsync("//home")
    └─ Không → Hiện lỗi → Quay lại form đăng nhập
```

## Điểm kiểm tra đã sửa
✅ Token được kiểm tra trước khi chuyển trang  
✅ Session chỉ được lưu khi login/register thành công  
✅ Token expiry rõ ràng (24 giờ)  
✅ Người dùng nhận phản hồi chính xác khi login thất bại  

## Hướng dùng API
- Email: `admin@rideapi.local`
- Mật khẩu: `Admin@123`
- Token có hiệu lực: 24 giờ
