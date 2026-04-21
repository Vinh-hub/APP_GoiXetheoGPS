# Web API chuẩn và mapping theo màn hình

## Cấu hình base URL
- Mặc định: `http://127.0.0.1:5000`
- Android Emulator: tự dùng `http://10.0.2.2:5000`
- Có thể override runtime qua `WebApiServerConfig.BaseUrl`

## Endpoint chuẩn

### 1) MainPage (Dashboard DB phân tán)
- `GET /api/distributed-db/stats` → lấy cả DB1 + DB2
- `GET /api/distributed-db/stats/primary` → lấy DB1
- `GET /api/distributed-db/stats/secondary` (hoặc `.../replica`) → lấy DB2

Fallback route tương thích:
- `/api/database/stats`
- `/api/distributeddb/stats`
- `/api/database/stats/primary`
- `/api/database/stats/secondary`

### 2) TripTrackingPage (Danh sách chuyến)
- `GET /api/trips`

Fallback route tương thích:
- `/api/trip`
- `/api/rides`

### 3) TripDetailPage (Chi tiết chuyến)
- `GET /api/trips/{id}`

Fallback route tương thích:
- `/api/trip/{id}`
- `/api/rides/{id}`

## Mapping code hiện tại
- `Pages/MainPage.xaml.cs` → `Services/DistributedDatabaseService`
- `Pages/TripTrackingPage.xaml.cs` → `Services/TripDataStore.GetGroupedByMonthAsync()`
- `Pages/TripDetailPage.xaml.cs` → `Services/TripDataStore.FindByIdAsync()`

Cả 3 màn đã được map sang gọi Web API (không đọc MySQL trực tiếp từ UI).

## ApiClient chung cho mô hình CSDL phân tán

- `Services/ApiClient.cs`
  - Tự gắn `Authorization: Bearer <JWT>` từ `AuthSessionService`
  - Tự gắn header `X-User-Latitude` từ GPS qua `UserLocationService`
  - Xử lý lỗi `503` thống nhất bằng `ApiReadOnlyException`

- `Services/ApiErrorHandler.cs`
  - Chuẩn hóa thông báo cho UI
  - Khi gặp `503`, UI nhận message: `Hệ thống đang ở chế độ chỉ đọc. Vui lòng thử lại sau.`

## Các service API mới

- `AuthApiService`
  - `POST /api/auth/login`
  - `POST /api/auth/register`
- `DriverApiService`
  - `GET /api/drivers/nearby`
  - `POST /api/drivers/update-location`
- `RideApiService`
  - `POST /api/rides`
  - `GET /api/rides/history`
- `PaymentApiService`
  - `POST /api/payment`
- `RatingApiService`
  - `POST /api/rating`
