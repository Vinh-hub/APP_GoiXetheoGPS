CREATE DATABASE NorthDB;
CREATE DATABASE SouthDB;
SET NAMES utf8mb4;

-- NORTHDB — Miền Bắc 
USE NorthDB;

CREATE TABLE Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions VALUES (1, 'North');

CREATE TABLE Customers (
    CustomerID INT AUTO_INCREMENT PRIMARY KEY,
    FullName   VARCHAR(100) NOT NULL,
    Phone      VARCHAR(20),
    Email      VARCHAR(100),
    Gender     VARCHAR(10),
    Birthday   DATE,
    CreatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP
);

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
    IsActive  TINYINT(1) NOT NULL DEFAULT 1, -- 1 = hoạt động, 0 = bị khóa
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID),
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID),
    UNIQUE KEY UQ_North_Users_Email (Email),
    -- 1-1: 1 Customer/Driver chỉ gắn tối đa 1 tài khoản Users
    UNIQUE KEY UQ_North_Users_CustomerID (CustomerID),
    UNIQUE KEY UQ_North_Users_DriverID (DriverID),
    -- Mỗi user chỉ thuộc đúng 1 loại (Customer hoặc Driver)
    CHECK (
        (Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
        (Role='Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL)
    )
);

CREATE TABLE Drivers (
    DriverID INT AUTO_INCREMENT PRIMARY KEY,
    Name     VARCHAR(100),
    Phone    VARCHAR(20),
    Status   VARCHAR(20),
    RegionID INT,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,  -- 1 = hoạt động, 0 = bị khóa
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

ALTER TABLE Users
ADD CONSTRAINT FK_North_Users_Drivers
FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID);

CREATE TABLE Vehicles (
    VehicleID   INT AUTO_INCREMENT PRIMARY KEY,
    DriverID    INT,
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE Trips (
    TripID    INT AUTO_INCREMENT PRIMARY KEY,
    UserID    INT,
    DriverID  INT,
    Status    VARCHAR(20),
    Price     DECIMAL(10,2),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID)   REFERENCES Users(UserID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE TripLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    TripID     INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    Address    VARCHAR(255),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    TripID    INT,
    Amount    DECIMAL(10,2),
    Method    VARCHAR(50),
    Status    VARCHAR(20),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Reviews (
    ReviewID INT AUTO_INCREMENT PRIMARY KEY,
    TripID   INT,
    Rating   INT,
    Comment  VARCHAR(500),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Promotions (
    PromoID    INT AUTO_INCREMENT PRIMARY KEY,
    Code       VARCHAR(50),
    Discount   INT,
    ExpiryDate DATETIME
);

CREATE TABLE RideRequests (
    RequestID      INT AUTO_INCREMENT PRIMARY KEY,
    UserID         INT,
    PickupLocation VARCHAR(255),
    Destination    VARCHAR(255),
    RequestTime    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE DriverLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID   INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    UpdatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

INSERT INTO Customers (FullName, Phone, Email) VALUES
('Nguyễn Văn An',  '0901234567', 'an.nguyen@gmail.com'),
('Trần Thị Bích',  '0912345678', 'bich.tran@gmail.com'),
('Lê Hoàng Nam',   '0923456789', 'locked_north@gmail.com');

INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('an.nguyen@gmail.com',    '123', 'Customer', 1, 'Nguyễn Văn An', '0901234567', 1, 1),
('bich.tran@gmail.com',    '123', 'Customer', 2, 'Trần Thị Bích', '0912345678', 1, 1),
('locked_north@gmail.com', '123', 'Customer', 3, 'Lê Hoàng Nam',  '0923456789', 1, 0);
 
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive) VALUES
('Phạm Minh Tuấn', '0934567890', 'Available', 1, 1),
('Đỗ Quang Huy',   '0945678901', 'Available', 1, 1),
('Vũ Đức Thắng',   '0956789012', 'Available', 1, 0); 

INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('tuan.driver@demo.com', '123', 'Driver', 1, 'Phạm Minh Tuấn', '0934567890', 1, 1),
('huy.driver@demo.com',  '123', 'Driver', 2, 'Đỗ Quang Huy',   '0945678901', 1, 1),
('thang.driver@demo.com','123', 'Driver', 3, 'Vũ Đức Thắng',   '0956789012', 1, 0);
 
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '29B1-123.45', 'Bike'),
(2, '30A-123.45',  'Car'),
(3, '29F1-234.56', 'Electric');
 
INSERT INTO Trips (UserID, DriverID, Status, Price) VALUES
(1, 1, 'Completed', 50000),
(2, 2, 'Requested', 70000);
 
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 50000, 'Cash', 'Paid');
 
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 5, 'Tài xế nhiệt tình, đúng giờ');
 
INSERT INTO DriverLocations (DriverID, Latitude, Longitude) VALUES
(1, 21.03, 105.85),
(2, 21.02, 105.84),
(3, 21.01, 105.83);

-- SOUTHDB — Miền Nam 
USE SouthDB;

CREATE TABLE Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);
INSERT INTO Regions VALUES (2, 'South');

CREATE TABLE Customers (
    CustomerID INT AUTO_INCREMENT PRIMARY KEY,
    FullName   VARCHAR(100) NOT NULL,
    Phone      VARCHAR(20),
    Email      VARCHAR(100),
    Gender     VARCHAR(10),
    Birthday   DATE,
    CreatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP
);

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
    IsActive  TINYINT(1) NOT NULL DEFAULT 1, -- 1 = hoạt động, 0 = bị khóa
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID),
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID),
    UNIQUE KEY UQ_South_Users_Email (Email),
    -- 1-1: 1 Customer/Driver chỉ gắn tối đa 1 tài khoản Users
    UNIQUE KEY UQ_South_Users_CustomerID (CustomerID),
    UNIQUE KEY UQ_South_Users_DriverID (DriverID),
    CHECK (
        (Role='Customer' AND CustomerID IS NOT NULL AND DriverID IS NULL) OR
        (Role='Driver' AND DriverID IS NOT NULL AND CustomerID IS NULL)
    )
);

CREATE TABLE Drivers (
    DriverID INT AUTO_INCREMENT PRIMARY KEY,
    Name     VARCHAR(100),
    Phone    VARCHAR(20),
    Status   VARCHAR(20),
    RegionID INT,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,  -- 1 = hoạt động, 0 = bị khóa
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

ALTER TABLE Users
ADD CONSTRAINT FK_South_Users_Drivers
FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID);

CREATE TABLE Vehicles (
    VehicleID   INT AUTO_INCREMENT PRIMARY KEY,
    DriverID    INT,
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE Trips (
    TripID    INT AUTO_INCREMENT PRIMARY KEY,
    UserID    INT,
    DriverID  INT,
    Status    VARCHAR(20),
    Price     DECIMAL(10,2),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID)   REFERENCES Users(UserID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE TripLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    TripID     INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    Address    VARCHAR(255),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    TripID    INT,
    Amount    DECIMAL(10,2),
    Method    VARCHAR(50),
    Status    VARCHAR(20),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Reviews (
    ReviewID INT AUTO_INCREMENT PRIMARY KEY,
    TripID   INT,
    Rating   INT,
    Comment  VARCHAR(500),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Promotions (
    PromoID    INT AUTO_INCREMENT PRIMARY KEY,
    Code       VARCHAR(50),
    Discount   INT,
    ExpiryDate DATETIME
);

CREATE TABLE RideRequests (
    RequestID      INT AUTO_INCREMENT PRIMARY KEY,
    UserID         INT,
    PickupLocation VARCHAR(255),
    Destination    VARCHAR(255),
    RequestTime    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE DriverLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID   INT,
    Latitude   FLOAT,
    Longitude  FLOAT,
    UpdatedAt  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

-- Seed Customers + Users (Customer accounts)
INSERT INTO Customers (FullName, Phone, Email) VALUES
('Huỳnh Thị Lan',   '0907654321', 'lan.huynh@gmail.com'),
('Võ Thanh Tùng',   '0918765432', 'tung.vo@gmail.com'),
('Ngô Thị Mai',     '0929876543', 'locked_south@gmail.com'); -- bị khóa (để test)

INSERT INTO Users (Email, Password, Role, CustomerID, Name, Phone, RegionID, IsActive) VALUES
('lan.huynh@gmail.com',    '123', 'Customer', 1, 'Huỳnh Thị Lan', '0907654321', 2, 1),
('tung.vo@gmail.com',      '123', 'Customer', 2, 'Võ Thanh Tùng', '0918765432', 2, 1),
('locked_south@gmail.com', '123', 'Customer', 3, 'Ngô Thị Mai',   '0929876543', 2, 0);
 
INSERT INTO Drivers (Name, Phone, Status, RegionID, IsActive) VALUES
('Trương Văn Khoa', '0938765432', 'Available', 2, 1),
('Bùi Thị Hoa',     '0949876543', 'Available', 2, 1),
('Đinh Quốc Bảo',   '0959876543', 'Available', 2, 0); -- bị khóa (để test)

-- Seed Users (Driver accounts)
INSERT INTO Users (Email, Password, Role, DriverID, Name, Phone, RegionID, IsActive) VALUES
('khoa.driver@demo.com', '123', 'Driver', 1, 'Trương Văn Khoa', '0938765432', 2, 1),
('hoa.driver@demo.com',  '123', 'Driver', 2, 'Bùi Thị Hoa',     '0949876543', 2, 1),
('bao.driver@demo.com',  '123', 'Driver', 3, 'Đinh Quốc Bảo',   '0959876543', 2, 0);
 
INSERT INTO Vehicles (DriverID, PlateNumber, VehicleType) VALUES
(1, '59B1-123.45', 'Bike'),
(2, '51A-123.45',  'Car'),
(3, '59F1-234.56', 'Electric');
 
INSERT INTO Trips (UserID, DriverID, Status, Price) VALUES
(1, 1, 'Completed', 45000),
(2, 2, 'Requested', 60000);
 
INSERT INTO Payments (TripID, Amount, Method, Status) VALUES
(1, 45000, 'Cash', 'Paid');
 
INSERT INTO Reviews (TripID, Rating, Comment) VALUES
(1, 4, 'Nhanh và đúng giờ');
 
INSERT INTO DriverLocations (DriverID, Latitude, Longitude) VALUES
(1, 10.78, 106.70),
(2, 10.76, 106.68),
(3, 10.77, 106.69);

SELECT 'NorthDB Users' AS '';  
USE NorthDB; 
SELECT * FROM Users;

SELECT 'NorthDB Drivers' AS ''; 
SELECT * FROM Drivers;

SELECT 'SouthDB Users' AS '';  
USE SouthDB; 
SELECT * FROM Users;

SELECT 'SouthDB Drivers' AS ''; 
SELECT * FROM Drivers;