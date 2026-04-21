-- ============================================
-- NORTH REGION (PostgreSQL version)
-- ============================================

-- Bảng Regions
CREATE TABLE IF NOT EXISTS Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions (RegionID, Name) VALUES (1, 'North') ON CONFLICT DO NOTHING;

-- Bảng Customers
CREATE TABLE IF NOT EXISTS Customers (
    CustomerID SERIAL PRIMARY KEY,
    FullName   VARCHAR(100) NOT NULL,
    Phone      VARCHAR(20),
    Email      VARCHAR(100),
    Gender     VARCHAR(10),
    Birthday   DATE,
    CreatedAt  TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Bảng Drivers
CREATE TABLE IF NOT EXISTS Drivers (
    DriverID SERIAL PRIMARY KEY,
    Name     VARCHAR(100),
    Phone    VARCHAR(20),
    Status   VARCHAR(20),
    RegionID INT REFERENCES Regions(RegionID),
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    AvgRating REAL DEFAULT 0
);

-- Bảng Users
CREATE TABLE IF NOT EXISTS Users (
    UserID     SERIAL PRIMARY KEY,
    Email      VARCHAR(100) NOT NULL UNIQUE,
    Password   VARCHAR(100) NOT NULL,
    Role       VARCHAR(20) NOT NULL CHECK (Role IN ('Customer', 'Driver')),
    CustomerID INT NULL UNIQUE REFERENCES Customers(CustomerID),
    DriverID   INT NULL UNIQUE REFERENCES Drivers(DriverID),
    Name       VARCHAR(100),
    Phone      VARCHAR(20),
    RegionID   INT REFERENCES Regions(RegionID),
    IsActive   BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CHECK ((Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
           (Role='Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL))
);

-- Bảng Vehicles
CREATE TABLE IF NOT EXISTS Vehicles (
    VehicleID   SERIAL PRIMARY KEY,
    DriverID    INT REFERENCES Drivers(DriverID),
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50)
);

-- Bảng Trips
CREATE TABLE IF NOT EXISTS Trips (
    TripID        SERIAL PRIMARY KEY,
    UserID        INT REFERENCES Users(UserID),
    DriverID      INT REFERENCES Drivers(DriverID),
    Status        VARCHAR(20),
    Price         DECIMAL(10,2),
    StartLat      REAL,
    StartLng      REAL,
    EndLat        REAL,
    EndLng        REAL,
    PaymentAmount DECIMAL(10,2) NULL,
    DriverRating  INT NULL,
    DriverComment VARCHAR(500) NULL,
    CreatedAt     TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Bảng TripLocations
CREATE TABLE IF NOT EXISTS TripLocations (
    LocationID SERIAL PRIMARY KEY,
    TripID     INT REFERENCES Trips(TripID),
    Latitude   REAL,
    Longitude  REAL,
    Address    VARCHAR(255)
);

-- Bảng Payments
CREATE TABLE IF NOT EXISTS Payments (
    PaymentID SERIAL PRIMARY KEY,
    TripID    INT REFERENCES Trips(TripID),
    Amount    DECIMAL(10,2),
    Method    VARCHAR(50),
    Status    VARCHAR(20),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Bảng Reviews
CREATE TABLE IF NOT EXISTS Reviews (
    ReviewID SERIAL PRIMARY KEY,
    TripID   INT REFERENCES Trips(TripID),
    Rating   INT,
    Comment  VARCHAR(500)
);

-- Bảng Promotions
CREATE TABLE IF NOT EXISTS Promotions (
    PromoID    SERIAL PRIMARY KEY,
    Code       VARCHAR(50),
    Discount   INT,
    ExpiryDate TIMESTAMP
);

-- Bảng RideRequests
CREATE TABLE IF NOT EXISTS RideRequests (
    RequestID      SERIAL PRIMARY KEY,
    UserID         INT REFERENCES Users(UserID),
    PickupLocation VARCHAR(255),
    Destination    VARCHAR(255),
    RequestTime    TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Bảng DriverLocations
CREATE TABLE IF NOT EXISTS DriverLocations (
    LocationID SERIAL PRIMARY KEY,
    DriverID   INT REFERENCES Drivers(DriverID),
    Latitude   REAL,
    Longitude  REAL,
    UpdatedAt  TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==================== SEED DỮ LIỆU ====================

-- Customers
INSERT INTO Customers (FullName, Phone, Email) VALUES
('Nguyễn Văn An',  '0901234567', 'an.nguyen@gmail.com'),
('Trần Thị Bích',  '0912345678', 'bich.tran@gmail.com'),
('Lê Hoàng Nam',   '0923456789', 'locked_north@gmail.com')
ON CONFLICT DO NOTHING;

-- Drivers
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive, AvgRating) VALUES
('Phạm Minh Tuấn', '0934567890', 'Available', 1, TRUE, 0),
('Đỗ Quang Huy',   '0945678901', 'Available', 1, TRUE, 0),
('Vũ Đức Thắng',   '0956789012', 'Available', 1, FALSE, 0)
ON CONFLICT DO NOTHING;

-- Users (Customer)
INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('an.nguyen@gmail.com',    '123', 'Customer', 1, 'Nguyễn Văn An', '0901234567', 1, TRUE),
('bich.tran@gmail.com',    '123', 'Customer', 2, 'Trần Thị Bích', '0912345678', 1, TRUE),
('locked_north@gmail.com', '123', 'Customer', 3, 'Lê Hoàng Nam',  '0923456789', 1, FALSE)
ON CONFLICT (Email) DO NOTHING;

-- Users (Driver)
INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('tuan.driver@demo.com', '123', 'Driver', 1, 'Phạm Minh Tuấn', '0934567890', 1, TRUE),
('huy.driver@demo.com',  '123', 'Driver', 2, 'Đỗ Quang Huy',   '0945678901', 1, TRUE),
('thang.driver@demo.com','123', 'Driver', 3, 'Vũ Đức Thắng',   '0956789012', 1, FALSE)
ON CONFLICT (Email) DO NOTHING;

-- Vehicles
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '29B1-123.45', 'Bike'),
(2, '30A-123.45',  'Car'),
(3, '29F1-234.56', 'Electric')
ON CONFLICT DO NOTHING;

-- Trips
INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, PaymentAmount, DriverRating, DriverComment, CreatedAt) VALUES
(1, 1, 'Completed', 50000, 21.0245, 105.8510, 21.2181, 105.7898, 50000, 5, 'Tài xế nhiệt tình, đúng giờ', '2025-01-10 08:30:00'),
(2, 2, 'Requested', 70000, 21.0284, 105.8010, 21.0500, 105.8800, NULL, NULL, NULL, '2025-01-11 09:15:00'),
(1, 3, 'Cancelled', 127000, 21.0180, 105.8020, 21.0280, 105.7980, NULL, NULL, NULL, '2025-01-12 10:00:00'),
(2, 1, 'Completed', 88000, 21.0240, 105.8520, 21.0800, 105.8700, 88000, 5, 'Rất tốt', '2025-01-13 11:20:00'),
(1, 2, 'Completed', 120000, 21.0010, 105.8200, 21.0100, 105.8350, 120000, 4, 'Ổn', '2025-01-14 14:45:00'),
(2, 3, 'Completed', 49000, 21.0300, 105.7600, 21.0250, 105.8450, 49000, 5, 'Nhanh', '2025-01-15 16:00:00')
ON CONFLICT DO NOTHING;

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
(6, 21.0250, 105.8450, 'AEON Long Biên')
ON CONFLICT DO NOTHING;

-- Payments
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 50000, 'Cash', 'Paid'),
(4, 88000, 'Cash', 'Paid'),
(5, 120000, 'Momo', 'Paid'),
(6, 49000, 'Cash', 'Paid')
ON CONFLICT DO NOTHING;

-- Reviews
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 5, 'Tài xế nhiệt tình, đúng giờ'),
(4, 5, 'Rất tốt'),
(5, 4, 'Ổn'),
(6, 5, 'Nhanh')
ON CONFLICT DO NOTHING;

-- Cập nhật AvgRating cho Drivers
UPDATE Drivers d
SET AvgRating = COALESCE(t.avg_rating, 0)
FROM (
    SELECT DriverID, AVG(DriverRating) AS avg_rating
    FROM Trips
    WHERE DriverRating IS NOT NULL
    GROUP BY DriverID
) t
WHERE d.DriverID = t.DriverID;

-- DriverLocations
INSERT INTO DriverLocations (DriverID, Latitude, Longitude) VALUES
(1, 21.03, 105.85),
(2, 21.02, 105.84),
(3, 21.01, 105.83)
ON CONFLICT DO NOTHING;

-- INDEX
CREATE INDEX IF NOT EXISTS idx_trips_user ON Trips(UserID);
CREATE INDEX IF NOT EXISTS idx_trips_driver ON Trips(DriverID);
CREATE INDEX IF NOT EXISTS idx_trips_status ON Trips(Status);
CREATE INDEX IF NOT EXISTS idx_drivers_status ON Drivers(Status);
CREATE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
CREATE INDEX IF NOT EXISTS idx_driverlocations_driver ON DriverLocations(DriverID);

SELECT 'NorthDB initialized successfully' AS message;

