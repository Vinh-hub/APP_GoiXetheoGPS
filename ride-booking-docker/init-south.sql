-- ============================================
-- SOUTH REGION (PostgreSQL version)
-- ============================================

-- Bảng Regions
CREATE TABLE IF NOT EXISTS Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions (RegionID, Name) VALUES (2, 'South') ON CONFLICT DO NOTHING;

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
('Huỳnh Thị Lan',   '0907654321', 'lan.huynh@gmail.com'),
('Võ Thanh Tùng',   '0918765432', 'tung.vo@gmail.com'),
('Ngô Thị Mai',     '0929876543', 'locked_south@gmail.com')
ON CONFLICT DO NOTHING;

-- Drivers
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive, AvgRating) VALUES
('Trương Văn Khoa', '0938765432', 'Available', 2, TRUE, 0),
('Bùi Thị Hoa',     '0949876543', 'Available', 2, TRUE, 0),
('Đinh Quốc Bảo',   '0959876543', 'Available', 2, FALSE, 0)
ON CONFLICT DO NOTHING;

-- Users (Customer)
INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('lan.huynh@gmail.com',    '123', 'Customer', 1, 'Huỳnh Thị Lan', '0907654321', 2, TRUE),
('tung.vo@gmail.com',      '123', 'Customer', 2, 'Võ Thanh Tùng', '0918765432', 2, TRUE),
('locked_south@gmail.com', '123', 'Customer', 3, 'Ngô Thị Mai',   '0929876543', 2, FALSE)
ON CONFLICT (Email) DO NOTHING;

-- Users (Driver)
INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('khoa.driver@demo.com', '123', 'Driver', 1, 'Trương Văn Khoa', '0938765432', 2, TRUE),
('hoa.driver@demo.com',  '123', 'Driver', 2, 'Bùi Thị Hoa',     '0949876543', 2, TRUE),
('bao.driver@demo.com',  '123', 'Driver', 3, 'Đinh Quốc Bảo',   '0959876543', 2, FALSE)
ON CONFLICT (Email) DO NOTHING;

-- Vehicles
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '59B1-123.45', 'Bike'),
(2, '51A-123.45',  'Car'),
(3, '59F1-234.56', 'Electric')
ON CONFLICT DO NOTHING;

-- Trips
INSERT INTO Trips (UserID, DriverID, Status, Price, StartLat, StartLng, EndLat, EndLng, PaymentAmount, DriverRating, DriverComment, CreatedAt) VALUES
(1, 1, 'Completed', 45000, 10.7945, 106.7219, 10.8188, 106.6519, 45000, 4, 'Nhanh và đúng giờ', '2025-02-01 07:30:00'),
(2, 2, 'Requested', 60000, 10.7769, 106.7009, 10.8501, 106.7719, NULL, NULL, NULL, '2025-02-02 09:00:00'),
(1, 3, 'Cancelled', 95000, 10.7720, 106.6980, 10.8150, 106.6680, NULL, NULL, NULL, '2025-02-03 10:45:00'),
(2, 1, 'Completed', 52000, 10.7600, 106.6800, 10.7300, 106.7180, 52000, 5, 'Chu đáo', '2025-02-04 13:15:00'),
(1, 2, 'Completed', 38000, 10.7800, 106.7050, 10.7900, 106.7120, 38000, 4, 'Ổn định', '2025-02-05 15:30:00')
ON CONFLICT DO NOTHING;

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
(5, 10.7900, 106.7120, 'Bình Thạnh')
ON CONFLICT DO NOTHING;

-- Payments
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 45000, 'Cash', 'Paid'),
(4, 52000, 'Cash', 'Paid'),
(5, 38000, 'Momo', 'Paid')
ON CONFLICT DO NOTHING;

-- Reviews
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 4, 'Nhanh và đúng giờ'),
(4, 5, 'Chu đáo'),
(5, 4, 'Ổn định')
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
(1, 10.78, 106.70),
(2, 10.76, 106.68),
(3, 10.77, 106.69)
ON CONFLICT DO NOTHING;

-- INDEX
CREATE INDEX IF NOT EXISTS idx_trips_user ON Trips(UserID);
CREATE INDEX IF NOT EXISTS idx_trips_driver ON Trips(DriverID);
CREATE INDEX IF NOT EXISTS idx_trips_status ON Trips(Status);
CREATE INDEX IF NOT EXISTS idx_drivers_status ON Drivers(Status);
CREATE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
CREATE INDEX IF NOT EXISTS idx_driverlocations_driver ON DriverLocations(DriverID);

SELECT 'SouthDB initialized successfully' AS message;

