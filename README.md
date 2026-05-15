# APP_GoiXetheoGPS

# Distributed Ride-Hailing Database System

### (Database Distributed + Replication + Failover)

## 1. Giới thiệu

Trong các ứng dụng **thời gian thực** như ứng dụng gọi xe (ride-hailing), **độ trễ mạng (network latency)** có ảnh hưởng lớn đến trải nghiệm người dùng.

Ví dụ:

* Người dùng ở **TP.HCM** truy cập tài nguyên gắn với **miền Bắc** sẽ gặp độ trễ cao hơn so với dữ liệu được phục vụ gần khu vực đó.
* Điều này làm tăng thời gian cho đăng nhập, truy vấn chuyến, đặt chuyến và các thao tác khác.

Cách triển khai:

* **Một** Web API ASP.NET Core (`RideAPI`) kết nối tới **bốn** chuỗi PostgreSQL theo vùng **North** và **South** (mỗi vùng có **Primary** và **Replica**).
* Ứng dụng **.NET MAUI** (`APP_GoiXetheoGPS`) gọi API đó; API chọn vùng và node CSDL theo vị trí hoặc tỉnh thành (header, query hoặc body) và theo thao tác **đọc** hay **ghi**.

Dữ liệu mẫu và cấu hình replication **PostgreSQL 16** (streaming standby) được đóng gói trong `APP_GoiXetheoGPS/ride-booking-docker/` (`docker-compose.north.yml`, `docker-compose.south.yml`, `init-north.sql`, `init-south.sql`, `enable-replication.sh` — file shell được compose mount vào `docker-entrypoint-initdb.d`).

Thư mục `APP_GoiXetheoGPS/infra/` chứa thêm script và cấu hình mẫu cho **MySQL** và **Keepalived**; phần này đứng riêng, **không** được `RideAPI` import hay gọi. Phần API chạy thật dùng **PostgreSQL** với **Npgsql**.

---

## 2. Mục tiêu của hệ thống

Mục tiêu của đồ án, bám sát phần đã triển khai trong dự án:

1. Thiết kế và minh họa **phân tách dữ liệu theo khu vực** (North / South) với hai cụm PostgreSQL độc lập trong Docker.
2. Cấu hình **nhân bản dữ liệu** (primary → standby) bằng **PostgreSQL streaming replication** trong `ride-booking-docker`.
3. Xử lý **sự cố primary** ở mức ứng dụng: **đọc** có thể chuyển sang **replica** khi primary không mở được kết nối; **ghi** yêu cầu primary (xem mục 7).
4. Khi không thể ghi vào primary, một số endpoint trả **HTTP 503** với nội dung thông báo **chế độ chỉ đọc** hoặc tương đương (xem mục 8).
5. Kiểm thử tay hoặc bám checklist trong `APP_GoiXetheoGPS/infra/tests/TEST_CHECKLIST.md` (file hướng dẫn, không kèm bộ test tự động).

Thiết kế hướng tới:

* Giảm độ trễ bằng cách gom thao tác theo vùng.
* Tận dụng replica cho **truy vấn đọc** khi primary tạm không khả dụng.
* Phân tách rõ hành vi **đọc** và **ghi** khi standby ở chế **hot standby** (chỉ đọc).

---

## 3. Kiến trúc hệ thống

Thành phần chính gồm:

* **Ứng dụng MAUI** (`APP_GoiXetheoGPS`) — target trong `APP_GoiXetheoGPS.csproj`: luôn có **Android** (`net10.0-android`); thêm **iOS** + **Mac Catalyst** khi build không phải Linux; thêm **Windows** (`net10.0-windows10.0.19041.0`) khi build trên Windows.
* **Web API kèm giao diện quản trị Razor** nằm ở thư mục **`RideAPI/RideAPI`** (cùng cấp với `APP_GoiXetheoGPS` trong repository — tức mở repo lên là thấy hai thư mục này nằm cạnh nhau). API chạy HTTP mặc định tại cổng **5136** (profile `http` trong `Properties/launchSettings.json`). Trong solution MAUI còn có thư mục `RideAPI` con bên trong `APP_GoiXetheoGPS`; file đó bị loại khỏi biên dịch app (`Compile Remove` trong `.csproj`), nên khi làm bài và chạy thử nên dùng **project ở gốc repo** như trên.
* **Bốn instance PostgreSQL** (hai cụm North và South, mỗi cụm primary + standby) khởi chạy qua Docker Compose trong `ride-booking-docker`.

### Kiến trúc tổng thể

```
                +----------------------+
                |   MAUI Client        |
                | APP_GoiXetheoGPS     |
                +----------+-----------+
                           |  HTTP (JWT)
                           |  Header X-User-Latitude / lat / province
                           v
                +----------------------+
                |   RideAPI (ASP.NET)  |
                | LocationRouting      |
                | Middleware + DB svc  |
                +--+--------+--------+--+
                   |        |        |
         North P/R |        |        | South P/R
            +-------v--+  +--v-------+
            | North    |  | South    |
            | Primary  |  | Primary  |
            +----|-----+  +-----|----+
                 |              |
            +----v-----+  +-----v----+
            | North    |  | South    |
            | Replica  |  | Replica  |
            +----------+  +----------+
```

---

## 4. Phân vùng dữ liệu (Data Partitioning)

Ở **Web API**, vùng `NORTH` hoặc `SOUTH` được xác định trong `LocationRoutingMiddleware` và `LocationRoutingService` như sau (thứ tự ưu tiên thực tế trong middleware):

1. Header `X-User-Latitude` (hoặc `x-user-latitude`) — nếu parse được vĩ độ thì áp dụng quy tắc vĩ độ (bước 3).
2. Query `province` hoặc `lat` trên các request `/api/...`.
3. Với **POST** hoặc **PUT** (không áp dụng **PATCH** trong mã hiện tại) có `Content-Type` `application/json` hoặc `application/x-www-form-urlencoded`, đọc `province` hoặc `lat` từ body (khi có).
4. Nếu vẫn không xác định được: mặc định **`SOUTH`** (middleware).

**Theo tỉnh/thành:** trong `LocationRoutingService`, tên tỉnh được chuẩn hóa (`Normalize`: bỏ dấu, xử lý gạch, gom khoảng trắng) rồi đối chiếu với hai tập cố định **`NorthLocations`** và **`SouthLocations`**. Danh sách đầy đủ nằm trong file mã; ở đây chỉ minh họa vài dạng sau khi chuẩn hóa: `ha noi`, `hanoi`, `hn`, `hai phong`, `ho chi minh`, `hcm`, `saigon`, `binh duong`, `can tho`, …

**Theo vĩ độ (khi không suy ra được từ tỉnh):**

* `latitude >= 16` → **`NORTH`**
* `latitude < 16` → **`SOUTH`**

Với các API có `[Authorize]`, một số controller **ưu tiên `regionId` trong JWT** (ví dụ `RidesController.ResolveRegionFromClaims`) thay vì chỉ nhìn tọa độ trên request — chi tiết nên đọc trực tiếp từng action trong controller.

---

## 5. Thiết kế Database

**RideAPI** dùng **PostgreSQL**. Các chuỗi kết nối tới bốn node (North/South, primary/replica) được khai báo trong file **`RideAPI/RideAPI/appsettings.json`**, mục **`DistributedDb`** — đúng với project API đặt ở gốc repository (cạnh thư mục app MAUI).

Script khởi tạo schema + seed nằm trong:

* `APP_GoiXetheoGPS/ride-booking-docker/init-north.sql`
* `APP_GoiXetheoGPS/ride-booking-docker/init-south.sql`

Các bảng được tạo trong các script trên (và được API/Dapper truy vấn):

| Table               | Mô tả ngắn gọn trong schema |
| ------------------- | ----------------------------- |
| Regions             | Vùng (1 North, 2 South)     |
| Customers           | Khách hàng                   |
| Drivers             | Tài xế                        |
| Users               | Tài khoản đăng nhập (Admin / Customer / Driver) |
| AuthRefreshTokens   | Refresh token (server)      |
| RevokedJwtTokens    | JWT đã thu hồi              |
| Vehicles            | Xe gắn tài xế               |
| Trips               | Chuyến (trạng thái, giá, tọa độ, …) |
| TripLocations       | Điểm/đoạn đường theo chuyến |
| Payments            | Thanh toán theo chuyến     |
| Reviews             | Đánh giá theo chuyến       |
| Promotions          | Khuyến mãi                  |
| RideRequests        | Yêu cầu chuyến (pickup/destination) |
| DriverLocations     | Vị trí tài xế               |

**Ghi chú:** Không có các bảng riêng tên `TripHistory`, `Locations`, `Notifications`, `Admins`, `Logs` như một số tài liệu cũ — lịch sử chuyến trong API đọc từ bảng **`Trips`** (ví dụ `GET /api/rides/history`). Tài khoản admin là các dòng `Users` với `Role = 'Admin'`.

---

## 6. Replication (Nhân bản dữ liệu)

Mỗi vùng trong Docker có:

* **Primary** (`north-master` port host **5432**, `south-master` port host **5434**)
* **Standby / replica** (`north-slave` port **5433**, `south-slave` port **5435**)

Cơ chế trong `docker-compose.*.yml`:

* Bật WAL phù hợp replica (`wal_level=replica`, `hot_standby=on`, …).
* Standby được tạo bằng `pg_basebackup` stream từ primary, chạy ở chế độ **read-only** (PostgreSQL hot standby).

**Khởi chạy hệ thống**

Thứ tự gợi ý: bật Docker cho hai cụm CSDL, sau đó chạy Web API. Cách đơn giản nhất là mở terminal **ở thư mục gốc của repository** (nơi vừa thấy `APP_GoiXetheoGPS` vừa thấy `RideAPI`). Khi đó lệnh `docker compose -f APP_GoiXetheoGPS/ride-booking-docker/...` trỏ đúng tới file compose, và `cd RideAPI/RideAPI` đưa vào đúng project mà `appsettings.json` đang cấu hình `Host=localhost` với các cổng **5432–5435** trùng với map port trong Docker.

```bash
docker compose -f APP_GoiXetheoGPS/ride-booking-docker/docker-compose.north.yml up -d
docker compose -f APP_GoiXetheoGPS/ride-booking-docker/docker-compose.south.yml up -d

cd RideAPI/RideAPI
dotnet run --launch-profile http
```

Nếu bạn đang đứng sẵn trong thư mục **`APP_GoiXetheoGPS`** (ví dụ mở terminal từ IDE tại đó), dùng đường dẫn compose ngắn hơn và **lùi một cấp** rồi vào API:

```bash
docker compose -f ride-booking-docker/docker-compose.north.yml up -d
docker compose -f ride-booking-docker/docker-compose.south.yml up -d

cd ../RideAPI/RideAPI
dotnet run --launch-profile http
```

Sau khi API chạy, trình duyệt hoặc công cụ test gọi **`http://localhost:5136`** (đúng profile `http` trong `launchSettings.json`). Ứng dụng MAUI trên **Android Emulator** mặc định trỏ tới **`http://10.0.2.2:5136`** — cấu hình này nằm trong `WebApiServerConfig.cs`.

---

## 7. Cơ chế Failover

Logic nằm trong `DatabaseService.GetConnectionAsync(string region, bool isWrite)`:

* **Đọc (`isWrite: false`):** thử mở **primary**; nếu thất bại thì thử **replica**; nếu cả hai thất bại → ném `InvalidOperationException` với message `ALL_DB_NODES_DOWN`.
* **Ghi (`isWrite: true`):** chỉ thử **primary**; nếu không mở được kết nối → ném `InvalidOperationException` với message **`MASTER_DOWN_CANNOT_WRITE`** (ứng dụng **không** tự chuyển ghi sang replica).

`DbRetryService` bọc Polly: retry khi lỗi transient của Npgsql, timeout, hoặc hai message lỗi ở trên.

Các controller **bắt cụ thể** `MASTER_DOWN_CANNOT_WRITE` và trả **HTTP 503** JSON: `RidesController` (đặt chuyến), `PaymentController`, `RatingController`. `TripService` (được `TripController` gọi) cũng dùng `DbRetryService` cho thao tác ghi nhưng **không** `catch` message này → sau hết lần retry có thể trả **500** thay vì 503 có cấu trúc. `DriversController` dùng `DatabaseService.GetConnection` (chỉ tạo kết nối tới **primary** của vùng, **không** fallback replica) — không đi qua nhánh `MASTER_DOWN_CANNOT_WRITE` ở `GetConnectionAsync`.

---

## 8. Chế độ Read-Only

* Standby PostgreSQL ở chế **hot standby**: không nhận lệnh ghi SQL từ client.
* Khi primary **chết** mà replica vẫn sống, phía **đọc** có thể chạy qua replica; các thao tác **ghi** vẫn cần primary nên dễ về **503** như trên. Repo **không** có bước **promote** standby lên primary.

Ví dụ nội dung JSON (đúng literal trong mã):

* `POST /api/rides` → `error`: `"Hệ thống đang ở chế độ chỉ đọc, không thể đặt chuyến"` (`RidesController`).
* Thanh toán / đánh giá → `error`: `"Thanh toán tạm thời không khả dụng (hệ thống chỉ đọc)"` / `"Đánh giá tạm thời không khả dụng (hệ thống chỉ đọc)"` (`PaymentController`, `RatingController`).

`POST /api/drivers/update-location` khi primary không mở được: **503** với `message` kiểu lỗi kết nối CSDL (`DriversController`, `NpgsqlException`) — **không** dùng các chuỗi `MASTER_DOWN_CANNOT_WRITE` ở trên.

**Đăng nhập** (`POST /api/auth/login`) gọi `GetConnectionAsync(..., isWrite: false)` nên vẫn đọc được tài khoản trên node nào đang mở được (primary hoặc replica), không rơi vào lỗi **thiếu primary** kiểu ghi dữ liệu.

---

## 9. Quy trình hoạt động của hệ thống

### Bước 1 — Người dùng mở ứng dụng MAUI

Cấu trình điều hướng trong `AppShell.xaml`: `AuthWelcomePage` (route `auth-welcome`), `LoginPage` (`auth-login`), `RegisterPage` (`auth-register`), **Trang chủ** `MainPage` (`home`), **Bản đồ** `HomeMapPage` (`map`), **Theo dõi chuyến** `TripTrackingPage` (`trips`), **Tài khoản** `AuthPage` (`auth`). Footer flyout: `SfSegmentedControl` (Syncfusion) chọn sáng/tối.

### Bước 2 — Định tuyến vùng trên API

Request có path bắt đầu `/api` đi qua `LocationRoutingMiddleware`, gán `context.Items["Region"]` theo quy tắc mục 4.

Client MAUI: `ApiClient`, `TripDataStore`, `DistributedDatabaseService` gắn **`Authorization: Bearer`** (từ `AuthSessionService`) và **`X-User-Latitude`** (khi `UserLocationService` có vĩ độ).

Base URL API: `WebApiServerConfig` (Preferences); mặc định Android `http://10.0.2.2:5136`, nền tảng khác `http://localhost:5136`. Token Mapbox Geocoding (trong `HomeMapPage`): thứ tự `MAPBOX_ACCESS_TOKEN` (biến môi trường) → MauiAsset `mapbox_token.txt` (`Resources/Raw/`) → Preferences — xem `MapboxConfig.cs`.

### Bước 3 — Thực hiện request

* REST JSON dưới prefix `/api/...` (Swagger bật trong môi trường **Development**).
* Giao diện quản trị Razor MVC dưới prefix `/admin/...` (đăng nhập admin, dashboard, quản lý user, …) — cookie `admin_jwt` được đọc trong cấu hình JWT Bearer.

### Bước 4 — Replication

Thay đổi trên **primary** được standby áp dụng qua replication PostgreSQL (xem mục 6).

### Bước 5 — Failover ở tầng đọc / lỗi ghi

Đọc: fallback primary → replica. Ghi: yêu cầu primary; nếu không có → lỗi và xử lý 503 tại các endpoint đã nêu.

---

## 10. Test Case

Các kịch bản dưới minh họa hành vi thực tế của app — thường kiểm tra tay hoặc qua Docker.

## Test Case 1 — Định tuyến vùng từ tỉnh

**Input (ví dụ query hoặc body):** `province` thuộc tập **South** trong `LocationRoutingService` (ví dụ chuỗi chuẩn hóa tương đương TP.HCM).

**Kết quả mong đợi:** middleware gán vùng **`SOUTH`** (trừ khi controller JWT ép `NORTH`/`SOUTH` theo `regionId` — áp dụng cho một số API).

---

## Test Case 2 — Replication

**Bước:** Chèn hoặc cập nhật một bản ghi trên **North primary** (port **5432**), sau đó truy vấn cùng dữ liệu trên **North replica** (port **5433**).

**Kết quả mong đợi:** Dữ liệu đọc được trên replica sau thời gian replication (streaming).

---

## Test Case 3 — Failover đọc

**Bước:** Dừng container primary một vùng, gọi API **chỉ đọc** tới vùng đó (ví dụ thống kê `GET /api/distributed-db/stats` — endpoint yêu cầu JWT).

**Kết quả mong đợi:** Nếu replica còn sống, `DatabaseService` mở được replica và trả dữ liệu; nếu cả hai chết, báo lỗi / DTO fallback tùy endpoint (xem `DistributedDbController`).

---

## Test Case 4 — Ghi khi primary chết

**Bước:** Dừng primary **South**, gọi `POST /api/rides` (có JWT khách hàng hợp lệ).

**Kết quả mong đợi:** **HTTP 503** với thông báo chỉ đọc (bắt `MASTER_DOWN_CANNOT_WRITE` trong `RidesController`).

---

## Test Case 5 — Query dữ liệu đọc được trên replica

**Bước:** `SELECT` đơn giản (ví dụ đếm `Users`) trên cổng replica khi primary đang chạy.

**Kết quả mong đợi:** Truy vấn **SELECT** thành công trên standby.

---

## 11. Công nghệ sử dụng

| Technology / thành phần | Vai trò trong dự án |
| ----------------------- | --------------------------- |
| PostgreSQL 16           | CSDL chính (Docker + Npgsql) |
| Docker Compose          | `ride-booking-docker` — hai file compose North/South |
| ASP.NET Core (.NET 10)  | Web API + MVC Admin; Swagger UI bật khi `IsDevelopment` (`Swashbuckle.AspNetCore` trong project Web API) |
| Npgsql (+ EF Core PostgreSQL) | Driver PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL` trong `RideAPI/RideAPI/RideAPI.csproj`) |
| Dapper                  | Truy vấn SQL trên `NpgsqlConnection` |
| Polly                   | Retry (`DbRetryService`)   |
| JWT Bearer              | Bảo vệ API; admin dùng thêm cookie `admin_jwt` / header `X-Jwt-Token` (xem `Program.cs`) |
| BCrypt.Net-Next         | Hash/verify mật khẩu (hỗ trợ tương thích mật khẩu seed dạng plaintext) |
| .NET MAUI (.NET 10)     | Ứng dụng khách `APP_GoiXetheoGPS` |
| CommunityToolkit.Maui / CommunityToolkit.Mvvm | UI helpers và MVVM (`APP_GoiXetheoGPS.csproj`) |
| Syncfusion.Maui.Toolkit | Điều khiển UI (ví dụ chọn theme) |
| Mapbox Geocoding API    | Gọi từ `HomeMapPage` — token: `MAPBOX_ACCESS_TOKEN` hoặc `Resources/Raw/mapbox_token.txt` hoặc Preferences (`MapboxConfig.cs`) |

**Ghi chú:** `APP_GoiXetheoGPS.csproj` khai báo `MySql.Data`, `Microsoft.Data.Sqlite.Core` và `SQLitePCLRaw.bundle_green`, nhưng trong mã MAUI hiện **chưa** thấy chỗ gọi trực tiếp các gói đó. `RideAPI/RideAPI/RideAPI.csproj` (ở gốc repo) có `MySqlConnector` và `Npgsql.EntityFrameworkCore.PostgreSQL` (kéo **Npgsql** transitively); truy vấn thực tế trong controller/service dùng **Dapper** + `NpgsqlConnection`/`NpgsqlCommand`.

---

## 12. Ưu điểm của hệ thống

✔ Giảm độ trễ theo vùng bằng cách tách cụm CSDL **North/South** và định tuyến request.

✔ Tận dụng **replica** cho truy vấn **đọc** khi primary tạm không kết nối được.

✔ Replication **streaming** chuẩn PostgreSQL, dễ tái tạo bằng Docker.

✔ API thống nhất (`RideAPI`) cho MAUI và giao diện admin.

---

## 13. Hạn chế

* Standby **không** nhận ghi; tầng app **không** tự promote standby lên primary mới.
* Định tuyến theo vĩ độ `>= 16` là **mô hình gọn**, không thay thế ranh giới hành chính đầy đủ.
* `GET /api/distributed-db/stats/primary` chỉ thống kê vùng **`NORTH`** (nhãn `"North Primary"`), không gộp luôn primary **South**.
* `GET /api/distributed-db/stats/secondary` (và `.../replica`) trong `DistributedDbController` chỉ thống kê một nhánh cố định (`SOUTH` + nhãn `"South Replica"`), nên tên URL dễ hiểu rộng hơn thực tế.
* `POST /api/auth/register` đang ghi `Users.Password` **đúng như** payload (chưa qua `HashPassword` ở nhánh insert đó); đăng nhập vẫn dùng `VerifyPassword` (BCrypt hoặc plaintext kiểu dữ liệu seed cũ).
* Một vài gói NuGet có thể là phụ thuộc dư — có thể gỡ khi dọn dependency.
* Môi trường **Development**: nếu thiếu `Jwt:Key`, `Program.cs` (`GetJwtKey`) dùng khóa cố định chỉ cho dev và in cảnh báo. **Không Development**: bắt buộc `Jwt:Key` ≥ **32** ký tự, nếu không ứng dụng không khởi động.

---

## 14. Hướng phát triển

Hướng có thể làm thêm sau này (ngoài phạm vi bản này):

* Multi-region database với orchestration promote/switchover.
* Load balancing tầng API và connection pool theo vùng.
* Auto failover / patroni / managed cloud (AWS RDS Multi-AZ, Azure Flexible Server, …).
* Dọn dẹp hoặc tích hợp thật các gói MySQL/SQLite nếu có nhu cầu lưu cục bộ trên thiết bị.

---

## 15. Kết luận

Tổng quan: **ứng dụng khách** viết bằng MAUI (`APP_GoiXetheoGPS`), **backend** là ASP.NET Core (`RideAPI/RideAPI` ở gốc repo), dữ liệu mẫu và hai cụm PostgreSQL chạy bằng Docker trong `APP_GoiXetheoGPS/ride-booking-docker/`. Thư mục `APP_GoiXetheoGPS/infra/` kèm ví dụ MySQL/Keepalived chỉ mang tính tham khảo, không nối vào luồng API chính.

Đồ án minh họa **phân vùng dữ liệu theo miền**, **nhân bản streaming trên PostgreSQL**, và phần **ứng xử đọc/ghi** khi primary không còn kết nối được — phù hợp báo cáo môn **cơ sở dữ liệu phân tán** hoặc làm nền cho bài toán gọi xe / dịch vụ theo vị trí.

---
