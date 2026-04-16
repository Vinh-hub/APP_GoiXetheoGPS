-- ============================================
-- NORTHDB (Miền Bắc) - Full fix (đã xử lý safe mode)
-- ============================================
DROP DATABASE IF EXISTS NorthDB;
CREATE DATABASE NorthDB CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE NorthDB;

-- Bảng Regions
CREATE TABLE Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions VALUES (1, 'North');

-- Bảng Customers
CREATE TABLE Customers (
    CustomerID INT AUTO_INCREMENT PRIMARY KEY,
    FullName   VARCHAR(100) NOT NULL,
    Phone      VARCHAR(20),
    Email      VARCHAR(100),
    Gender     VARCHAR(10),
    Birthday   DATE,
    CreatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Bảng Drivers (đã thêm AvgRating)
CREATE TABLE Drivers (
    DriverID INT AUTO_INCREMENT PRIMARY KEY,
    Name     VARCHAR(100),
    Phone    VARCHAR(20),
    Status   VARCHAR(20),
    RegionID INT,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    AvgRating FLOAT DEFAULT 0,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

-- Bảng Users
CREATE TABLE Users (
    UserID    INT AUTO_INCREMENT PRIMARY KEY,
    Email     VARCHAR(100) NOT NULL,
    Password  VARCHAR(100) NOT NULL,
    Role      ENUM('Customer','Driver') NOT NULL,
    CustomerID INT NULL,
    DriverID   INT NULL,
    Name      VARCHAR(100), 
    Phone     VARCHAR(20),  
    RegionID  INT,
    IsActive  TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID),
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID),
    UNIQUE KEY UQ_North_Users_Email (Email),
    UNIQUE KEY UQ_North_Users_CustomerID (CustomerID),
    UNIQUE KEY UQ_North_Users_DriverID (DriverID),
    CHECK ( (Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
            (Role='Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL) )
);

-- Bảng Vehicles
CREATE TABLE Vehicles (
    VehicleID   INT AUTO_INCREMENT PRIMARY KEY,
    DriverID    INT,
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- Bảng Trips (đã thêm các cột cần thiết)
CREATE TABLE Trips (
    TripID    INT AUTO_INCREMENT PRIMARY KEY,
    UserID    INT,
    DriverID  INT,
    Status    VARCHAR(20),
    Price     DECIMAL(10,2),
    StartLat  FLOAT,
    StartLng  FLOAT,
    EndLat    FLOAT,
    EndLng    FLOAT,
    PaymentAmount DECIMAL(10,2) NULL,
    DriverRating INT NULL,
    DriverComment VARCHAR(500) NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID)   REFERENCES Users(UserID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- Bảng TripLocations
CREATE TABLE TripLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    TripID     INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    Address    VARCHAR(255),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Payments
CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    TripID    INT,
    Amount    DECIMAL(10,2),
    Method    VARCHAR(50),
    Status    VARCHAR(20),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Reviews
CREATE TABLE Reviews (
    ReviewID INT AUTO_INCREMENT PRIMARY KEY,
    TripID   INT,
    Rating   INT,
    Comment  VARCHAR(500),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Promotions
CREATE TABLE Promotions (
    PromoID    INT AUTO_INCREMENT PRIMARY KEY,
    Code       VARCHAR(50),
    Discount   INT,
    ExpiryDate DATETIME
);

-- Bảng RideRequests
CREATE TABLE RideRequests (
    RequestID      INT AUTO_INCREMENT PRIMARY KEY,
    UserID         INT,
    PickupLocation VARCHAR(255),
    Destination    VARCHAR(255),
    RequestTime    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

-- Bảng DriverLocations
CREATE TABLE DriverLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID   INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    UpdatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- ==================== SEED DỮ LIỆU ====================

-- Customers
INSERT INTO Customers (FullName, Phone, Email) VALUES
('Nguyễn Văn An',  '0901234567', 'an.nguyen@gmail.com'),
('Trần Thị Bích',  '0912345678', 'bich.tran@gmail.com'),
('Lê Hoàng Nam',   '0923456789', 'locked_north@gmail.com');

-- Drivers (thêm AvgRating tạm 0, sẽ cập nhật sau)
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive, AvgRating) VALUES
('Phạm Minh Tuấn', '0934567890', 'Available', 1, 1, 0),
('Đỗ Quang Huy',   '0945678901', 'Available', 1, 1, 0),
('Vũ Đức Thắng',   '0956789012', 'Available', 1, 0, 0);

-- Users (Customer)
INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('an.nguyen@gmail.com',    '123', 'Customer', 1, 'Nguyễn Văn An', '0901234567', 1, 1),
('bich.tran@gmail.com',    '123', 'Customer', 2, 'Trần Thị Bích', '0912345678', 1, 1),
('locked_north@gmail.com', '123', 'Customer', 3, 'Lê Hoàng Nam',  '0923456789', 1, 0);

-- Users (Driver)
INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('tuan.driver@demo.com', '123', 'Driver', 1, 'Phạm Minh Tuấn', '0934567890', 1, 1),
('huy.driver@demo.com',  '123', 'Driver', 2, 'Đỗ Quang Huy',   '0945678901', 1, 1),
('thang.driver@demo.com','123', 'Driver', 3, 'Vũ Đức Thắng',   '0956789012', 1, 0);

-- Vehicles
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '29B1-123.45', 'Bike'),
(2, '30A-123.45',  'Car'),
(3, '29F1-234.56', 'Electric');

-- Trips
INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, PaymentAmount, DriverRating, DriverComment, CreatedAt) VALUES
(1, 1, 'Completed', 50000, 21.0245, 105.8510, 21.2181, 105.7898, 50000, 5, 'Tài xế nhiệt tình, đúng giờ', '2025-01-10 08:30:00'),
(2, 2, 'Requested', 70000, 21.0284, 105.8010, 21.0500, 105.8800, NULL, NULL, NULL, '2025-01-11 09:15:00'),
(1, 3, 'Cancelled', 127000, 21.0180, 105.8020, 21.0280, 105.7980, NULL, NULL, NULL, '2025-01-12 10:00:00'),
(2, 1, 'Completed', 88000, 21.0240, 105.8520, 21.0800, 105.8700, 88000, 5, 'Rất tốt', '2025-01-13 11:20:00'),
(1, 2, 'Completed', 120000, 21.0010, 105.8200, 21.0100, 105.8350, 120000, 4, 'Ổn', '2025-01-14 14:45:00'),
(2, 3, 'Completed', 49000, 21.0300, 105.7600, 21.0250, 105.8450, 49000, 5, 'Nhanh', '2025-01-15 16:00:00');

-- TripLocations
INSERT INTO TripLocations (TripID, Latitude, Longitude, Address) VALUES
(1, 21.0245, 105.8510, 'Vincom Bà Triệu, Hai Bà Trưng'),
(1, 21.2181, 105.7898, 'Sân bay Nội Bài'),
(2, 21.0284, 105.8010, 'Công ty ABC, Cầu Giấy'),
(2, 21.0500, 105.8800, 'Nhà riêng, Long Biên'),
(3, 21.0180, 105.8020, 'Big C Thăng Long'),
(3, 21.0280, 105.7980, 'Cầu Giấy'),
(4, 21.0240, 105.8520, 'Hồ Gươm'),
(4, 21.0800, 105.8700, 'Vinhomes Riverside'),
(5, 21.0010, 105.8200, 'Royal City'),
(5, 21.0100, 105.8350, 'Times City'),
(6, 21.0300, 105.7600, 'Lotte Center'),
(6, 21.0250, 105.8450, 'AEON Long Biên');

-- Payments
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 50000, 'Cash', 'Paid'),
(4, 88000, 'Cash', 'Paid'),
(5, 120000, 'Momo', 'Paid'),
(6, 49000, 'Cash', 'Paid');

-- Reviews
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 5, 'Tài xế nhiệt tình, đúng giờ'),
(4, 5, 'Rất tốt'),
(5, 4, 'Ổn'),
(6, 5, 'Nhanh');

-- Cập nhật AvgRating cho Drivers (tắt safe mode tạm thời)
SET SQL_SAFE_UPDATES = 0;

UPDATE Drivers d
LEFT JOIN (
    SELECT DriverID, AVG(DriverRating) AS avg_rating
    FROM Trips
    WHERE DriverRating IS NOT NULL
    GROUP BY DriverID
) t ON d.DriverID = t.DriverID
SET d.AvgRating = COALESCE(t.avg_rating, 0);

SET SQL_SAFE_UPDATES = 1;

-- DriverLocations
INSERT INTO DriverLocations (DriverID, Latitude, Longitude) VALUES
(1, 21.03, 105.85),
(2, 21.02, 105.84),
(3, 21.01, 105.83);

-- INDEX
CREATE INDEX idx_trips_user ON Trips(UserID);
CREATE INDEX idx_trips_driver ON Trips(DriverID);
CREATE INDEX idx_trips_status ON Trips(Status);
CREATE INDEX idx_drivers_status ON Drivers(Status);
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_driverlocations_driver ON DriverLocations(DriverID);

SELECT 'NorthDB fixed successfully' AS '';

-- ============================================
-- SOUTHDB (Miền Nam) - Full fix (đã xử lý safe mode)
-- ============================================
DROP DATABASE IF EXISTS SouthDB;
CREATE DATABASE SouthDB CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE SouthDB;

-- Bảng Regions
CREATE TABLE Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions VALUES (2, 'South');

-- Bảng Customers
CREATE TABLE Customers (
    CustomerID INT AUTO_INCREMENT PRIMARY KEY,
    FullName   VARCHAR(100) NOT NULL,
    Phone      VARCHAR(20),
    Email      VARCHAR(100),
    Gender     VARCHAR(10),
    Birthday   DATE,
    CreatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Bảng Drivers
CREATE TABLE Drivers (
    DriverID INT AUTO_INCREMENT PRIMARY KEY,
    Name     VARCHAR(100),
    Phone    VARCHAR(20),
    Status   VARCHAR(20),
    RegionID INT,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    AvgRating FLOAT DEFAULT 0,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

-- Bảng Users
CREATE TABLE Users (
    UserID    INT AUTO_INCREMENT PRIMARY KEY,
    Email     VARCHAR(100) NOT NULL,
    Password  VARCHAR(100) NOT NULL,
    Role      ENUM('Customer','Driver') NOT NULL,
    CustomerID INT NULL,
    DriverID   INT NULL,
    Name      VARCHAR(100), 
    Phone     VARCHAR(20),  
    RegionID  INT,
    IsActive  TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID),
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID),
    UNIQUE KEY UQ_South_Users_Email (Email),
    UNIQUE KEY UQ_South_Users_CustomerID (CustomerID),
    UNIQUE KEY UQ_South_Users_DriverID (DriverID),
    CHECK ( (Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
            (Role='Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL) )
);

-- Bảng Vehicles
CREATE TABLE Vehicles (
    VehicleID   INT AUTO_INCREMENT PRIMARY KEY,
    DriverID    INT,
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- Bảng Trips
CREATE TABLE Trips (
    TripID    INT AUTO_INCREMENT PRIMARY KEY,
    UserID    INT,
    DriverID  INT,
    Status    VARCHAR(20),
    Price     DECIMAL(10,2),
    StartLat  FLOAT,
    StartLng  FLOAT,
    EndLat    FLOAT,
    EndLng    FLOAT,
    PaymentAmount DECIMAL(10,2) NULL,
    DriverRating INT NULL,
    DriverComment VARCHAR(500) NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID)   REFERENCES Users(UserID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- Bảng TripLocations
CREATE TABLE TripLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    TripID     INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    Address    VARCHAR(255),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Payments
CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    TripID    INT,
    Amount    DECIMAL(10,2),
    Method    VARCHAR(50),
    Status    VARCHAR(20),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Reviews
CREATE TABLE Reviews (
    ReviewID INT AUTO_INCREMENT PRIMARY KEY,
    TripID   INT,
    Rating   INT,
    Comment  VARCHAR(500),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

-- Bảng Promotions
CREATE TABLE Promotions (
    PromoID    INT AUTO_INCREMENT PRIMARY KEY,
    Code       VARCHAR(50),
    Discount   INT,
    ExpiryDate DATETIME
);

-- Bảng RideRequests
CREATE TABLE RideRequests (
    RequestID      INT AUTO_INCREMENT PRIMARY KEY,
    UserID         INT,
    PickupLocation VARCHAR(255),
    Destination    VARCHAR(255),
    RequestTime    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

-- Bảng DriverLocations
CREATE TABLE DriverLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID   INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    UpdatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- ==================== SEED DỮ LIỆU ====================

-- Customers
INSERT INTO Customers (FullName, Phone, Email) VALUES
('Huỳnh Thị Lan',   '0907654321', 'lan.huynh@gmail.com'),
('Võ Thanh Tùng',   '0918765432', 'tung.vo@gmail.com'),
('Ngô Thị Mai',     '0929876543', 'locked_south@gmail.com');

-- Drivers
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive, AvgRating) VALUES
('Trương Văn Khoa', '0938765432', 'Available', 2, 1, 0),
('Bùi Thị Hoa',     '0949876543', 'Available', 2, 1, 0),
('Đinh Quốc Bảo',   '0959876543', 'Available', 2, 0, 0);

-- Users (Customer)
INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('lan.huynh@gmail.com',    '123', 'Customer', 1, 'Huỳnh Thị Lan', '0907654321', 2, 1),
('tung.vo@gmail.com',      '123', 'Customer', 2, 'Võ Thanh Tùng', '0918765432', 2, 1),
('locked_south@gmail.com', '123', 'Customer', 3, 'Ngô Thị Mai',   '0929876543', 2, 0);

-- Users (Driver)
INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('khoa.driver@demo.com', '123', 'Driver', 1, 'Trương Văn Khoa', '0938765432', 2, 1),
('hoa.driver@demo.com',  '123', 'Driver', 2, 'Bùi Thị Hoa',     '0949876543', 2, 1),
('bao.driver@demo.com',  '123', 'Driver', 3, 'Đinh Quốc Bảo',   '0959876543', 2, 0);

-- Vehicles
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '59B1-123.45', 'Bike'),
(2, '51A-123.45',  'Car'),
(3, '59F1-234.56', 'Electric');

-- Trips
INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, PaymentAmount, DriverRating, DriverComment, CreatedAt) VALUES
(1, 1, 'Completed', 45000, 10.7945, 106.7219, 10.8188, 106.6519, 45000, 4, 'Nhanh và đúng giờ', '2025-02-01 07:30:00'),
(2, 2, 'Requested', 60000, 10.7769, 106.7009, 10.8501, 106.7719, NULL, NULL, NULL, '2025-02-02 09:00:00'),
(1, 3, 'Cancelled', 95000, 10.7720, 106.6980, 10.8150, 106.6680, NULL, NULL, NULL, '2025-02-03 10:45:00'),
(2, 1, 'Completed', 52000, 10.7600, 106.6800, 10.7300, 106.7180, 52000, 5, 'Chu đáo', '2025-02-04 13:15:00'),
(1, 2, 'Completed', 38000, 10.7800, 106.7050, 10.7900, 106.7120, 38000, 4, 'Ổn định', '2025-02-05 15:30:00');

-- TripLocations
INSERT INTO TripLocations (TripID, Latitude, Longitude, Address) VALUES
(1, 10.7945, 106.7219, 'Landmark 81, Bình Thạnh'),
(1, 10.8188, 106.6519, 'Tân Sơn Nhất'),
(2, 10.7769, 106.7009, 'Quận 1, TP.HCM'),
(2, 10.8501, 106.7719, 'Thủ Đức'),
(3, 10.7720, 106.6980, 'Chợ Gò Vấp'),
(3, 10.8150, 106.6680, 'Sân bay TSN'),
(4, 10.7600, 106.6800, 'Thủ Đức'),
(4, 10.7300, 106.7180, 'Quận 7'),
(5, 10.7800, 106.7050, 'Quận 10'),
(5, 10.7900, 106.7120, 'Bình Thạnh');

-- Payments
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 45000, 'Cash', 'Paid'),
(4, 52000, 'Cash', 'Paid'),
(5, 38000, 'Momo', 'Paid');

-- Reviews
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 4, 'Nhanh và đúng giờ'),
(4, 5, 'Chu đáo'),
(5, 4, 'Ổn định');

-- Cập nhật AvgRating cho Drivers (tắt safe mode)
SET SQL_SAFE_UPDATES = 0;

UPDATE Drivers d
LEFT JOIN (
    SELECT DriverID, AVG(DriverRating) AS avg_rating
    FROM Trips
    WHERE DriverRating IS NOT NULL
    GROUP BY DriverID
) t ON d.DriverID = t.DriverID
SET d.AvgRating = COALESCE(t.avg_rating, 0);

SET SQL_SAFE_UPDATES = 1;

-- DriverLocations
INSERT INTO DriverLocations (DriverID, Latitude, Longitude) VALUES
(1, 10.78, 106.70),
(2, 10.76, 106.68),
(3, 10.77, 106.69);

-- INDEX
CREATE INDEX idx_trips_user ON Trips(UserID);
CREATE INDEX idx_trips_driver ON Trips(DriverID);
CREATE INDEX idx_trips_status ON Trips(Status);
CREATE INDEX idx_drivers_status ON Drivers(Status);
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_driverlocations_driver ON DriverLocations(DriverID);

SELECT 'SouthDB fixed successfully' AS '';

USE NorthDB;
UPDATE Users SET Password = '123' WHERE Email = 'an.nguyen@gmail.com';