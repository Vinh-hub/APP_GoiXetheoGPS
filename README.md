# APP_GoiXetheoGPS

# Distributed Ride-Hailing Database System

### (Database Distributed + Replication + Failover)

## 1. Giới thiệu

Trong các ứng dụng **thời gian thực** như ứng dụng gọi xe (ride-hailing), **độ trễ mạng (network latency)** có ảnh hưởng lớn đến trải nghiệm người dùng.

Ví dụ:

* Người dùng ở **TP.HCM** truy cập server đặt tại **Hà Nội** sẽ gặp độ trễ cao.
* Điều này làm tăng thời gian:

  * đăng nhập
  * tìm xe
  * đặt chuyến
  * truy vấn dữ liệu

Để giảm độ trễ và tăng khả năng chịu lỗi, hệ thống được thiết kế theo mô hình:

**Distributed Database System (Cơ sở dữ liệu phân tán)** kết hợp với:

* **Master–Slave Replication**
* **Failover Mechanism**
* **Read-only Backup Mode**

Hệ thống phân chia dữ liệu theo **khu vực địa lý (North / South)** nhằm tối ưu hiệu năng truy cập.

---

# 2. Mục tiêu của hệ thống

Mục tiêu của đồ án:

1. Thiết kế hệ thống **cơ sở dữ liệu phân tán theo khu vực địa lý**
2. Cấu hình **Replication (Primary → Replica)**
3. Xây dựng cơ chế **Failover khi server chính bị lỗi**
4. Cho phép **Read-only truy cập từ server backup**
5. Kiểm thử hệ thống bằng **Test Cases**

Hệ thống đảm bảo:

* Giảm độ trễ truy cập
* Tăng tính sẵn sàng (High Availability)
* Tăng khả năng chịu lỗi (Fault Tolerance)

---

# 3. Kiến trúc hệ thống

Hệ thống gồm:

* Application Client
* API Server
* Database Server (North / South)
* Replica Servers

### Kiến trúc tổng thể

```
                +----------------------+
                |     Application      |
                |  (Mobile / Web App)  |
                +----------+-----------+
                           |
                     Location Data
                           |
            +--------------+--------------+
            |                             |
     +------v------+              +-------v------+
     |  North API  |              |  South API   |
     +------|------+              +-------|------+
            |                             |
     +------v-------+             +-------v------+
     | North Primary|             | South Primary|
     |   Database   |             |   Database   |
     +------|-------+             +-------|------+
            |                             |
     +------v-------+             +-------v------+
     |North Replica |             |South Replica |
     | (Read Only)  |             | (Read Only)  |
     +--------------+             +--------------+
```

---

# 4. Phân vùng dữ liệu (Data Partitioning)

Dữ liệu được phân theo **khu vực địa lý** dựa trên **GPS hoặc Tỉnh/Thành phố**.

| Khu vực    | Server       |
| ---------- | ------------ |
| Hà Nội     | North Server |
| Hải Phòng  | North Server |
| Quảng Ninh | North Server |
| TP.HCM     | South Server |
| Bình Dương | South Server |
| Đồng Nai   | South Server |

Ví dụ định tuyến:

```
User Location: Ho Chi Minh City

→ Connect to South Server
```

Điều này giúp:

* giảm độ trễ
* giảm tải cho server
* tăng tốc độ truy vấn

---

# 5. Thiết kế Database

Hệ thống sử dụng **MySQL**.

Các bảng chính:

| Table         | Description          |
| ------------- | -------------------- |
| Users         | Thông tin người dùng |
| Drivers       | Thông tin tài xế     |
| Vehicles      | Thông tin xe         |
| Trips         | Chuyến đi            |
| TripHistory   | Lịch sử chuyến       |
| Locations     | Tọa độ GPS           |
| Payments      | Thanh toán           |
| Ratings       | Đánh giá             |
| Promotions    | Khuyến mãi           |
| Notifications | Thông báo            |
| Admins        | Quản trị hệ thống    |
| Logs          | Nhật ký hệ thống     |

Tổng cộng:

**12 bảng dữ liệu**

---

# 6. Replication (Nhân bản dữ liệu)

Mỗi khu vực có:

* **Primary Server**
* **Replica Server**

Replica được đồng bộ dữ liệu từ Primary thông qua:

**MySQL Master-Slave Replication**

Ví dụ:

```
North_Primary
      ↓
North_Replica
```

```
South_Primary
      ↓
South_Replica
```

Cơ chế hoạt động:

1. Primary ghi dữ liệu vào **Binary Log**
2. Replica đọc Binary Log
3. Replica thực hiện các thay đổi giống Primary

Điều này giúp:

* sao lưu dữ liệu
* tăng độ an toàn hệ thống

---

# 7. Cơ chế Failover

Failover là cơ chế **chuyển sang server dự phòng khi server chính gặp sự cố**.

Quy trình:

1. Application gửi request đến **Primary Server**
2. Nếu kết nối thất bại
3. Application tự động chuyển sang **Replica Server**

Ví dụ:

```
South_Primary DOWN
```

Application sẽ kết nối:

```
South_Replica
```

Điều này giúp hệ thống **không bị gián đoạn dịch vụ**.

---

# 8. Chế độ Read-Only

Khi hệ thống kết nối vào **Replica Server**, database sẽ hoạt động ở chế độ:

```
READ ONLY
```

Cho phép:

| Chức năng            | Trạng thái |
| -------------------- | ---------- |
| Đăng nhập            | ✅          |
| Xem lịch sử chuyến   | ✅          |
| Xem thông tin tài xế | ✅          |
| Đặt chuyến mới       | ❌          |
| Cập nhật dữ liệu     | ❌          |

Ví dụ:

Query thành công:

```
SELECT * FROM TripHistory
```

Query bị từ chối:

```
INSERT INTO Trips
```

---

# 9. Quy trình hoạt động của hệ thống

### Bước 1 — Người dùng mở ứng dụng

Ứng dụng lấy:

* GPS
* hoặc Tỉnh/Thành phố

---

### Bước 2 — Định tuyến server

Ví dụ:

```
City = Ho Chi Minh
```

→ Kết nối

```
South Server
```

---

### Bước 3 — Thực hiện request

Application gửi request:

```
API → Database
```

---

### Bước 4 — Replication

Dữ liệu được đồng bộ:

```
Primary → Replica
```

---

### Bước 5 — Failover

Nếu Primary bị lỗi:

```
Switch → Replica
```

---

# 10. Test Case

## Test Case 1 — Định tuyến server

Input

```
City = Ho Chi Minh
```

Expected Result

```
Connect → South Server
```

---

## Test Case 2 — Replication

Step

```
Insert Trip vào South Primary
```

Check

```
South Replica có dữ liệu giống
```

Expected Result

```
Replication hoạt động
```

---

## Test Case 3 — Failover

Step

```
Stop South Primary
```

Expected Result

```
Application connect → South Replica
```

---

## Test Case 4 — Read Only

Step

```
Insert Trip vào Replica
```

Expected Result

```
ERROR: Database Read Only
```

---

## Test Case 5 — Query dữ liệu

Step

```
SELECT * FROM TripHistory
```

Expected Result

```
Query thành công
```

---

# 11. Công nghệ sử dụng

| Technology        | Purpose            |
| ----------------- | ------------------ |
| MySQL             | Database           |
| MySQL Replication | Data replication   |
| .NET MAUI         | Mobile Application |
| ASP.NET Core      | Backend API        |
| GitHub            | Version Control    |
| XAMPP             | Local MySQL Server |

---

# 12. Ưu điểm của hệ thống

✔ Giảm **network latency**
✔ Tăng **high availability**
✔ Hỗ trợ **failover**
✔ Tăng **khả năng mở rộng (scalability)**

---

# 13. Hạn chế

* Replica chỉ hỗ trợ **read-only**
* Failover chưa tự động hoàn toàn
* Cần cơ chế **load balancer** nếu hệ thống lớn hơn

---

# 14. Hướng phát triển

Trong tương lai có thể mở rộng:

* Multi-region database
* Load balancing
* Auto failover
* Cloud deployment (AWS / GCP / Azure)

---

# 15. Kết luận

Đồ án đã xây dựng thành công một hệ thống:

**Distributed Database + Replication + Failover**

Hệ thống giúp:

* giảm độ trễ truy cập
* tăng tính sẵn sàng
* đảm bảo dữ liệu không bị mất khi server gặp sự cố

Giải pháp này phù hợp với các hệ thống:

* Ride-hailing (Grab, Gojek)
* Food Delivery
* Logistics
* Real-time services

---

