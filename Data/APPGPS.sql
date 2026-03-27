CREATE DATABASE NorthDB;
CREATE DATABASE SouthDB;

USE NorthDB;

USE SouthDB;

CREATE TABLE Regions (
    RegionID INT PRIMARY KEY,
    Name VARCHAR(50)
);

INSERT INTO Regions VALUES (1,'North'),(2,'South');

CREATE TABLE Users (
    UserID INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100),
    Phone VARCHAR(20),
    Email VARCHAR(100),
    Password VARCHAR(100),
    RegionID INT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

CREATE TABLE Drivers (
    DriverID INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100),
    Phone VARCHAR(20),
    Status VARCHAR(20),
    RegionID INT,
    FOREIGN KEY (RegionID) REFERENCES Regions(RegionID)
);

CREATE TABLE Vehicles (
    VehicleID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID INT,
    PlateNumber VARCHAR(20),
    VehicleType VARCHAR(50),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE Trips (
    TripID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT,
    DriverID INT,
    Status VARCHAR(20),
    Price DECIMAL(10,2),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID),
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE TripLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    TripID INT,
    Latitude FLOAT,
    Longitude FLOAT,
    Address VARCHAR(255),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    TripID INT,
    Amount DECIMAL(10,2),
    Method VARCHAR(50),
    Status VARCHAR(20),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Reviews (
    ReviewID INT AUTO_INCREMENT PRIMARY KEY,
    TripID INT,
    Rating INT,
    Comment VARCHAR(500),
    FOREIGN KEY (TripID) REFERENCES Trips(TripID)
);

CREATE TABLE Promotions (
    PromoID INT AUTO_INCREMENT PRIMARY KEY,
    Code VARCHAR(50),
    Discount INT,
    ExpiryDate DATETIME
);

CREATE TABLE RideRequests (
    RequestID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT,
    PickupLocation VARCHAR(255),
    Destination VARCHAR(255),
    RequestTime DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE DriverLocations (
    LocationID INT AUTO_INCREMENT PRIMARY KEY,
    DriverID INT,
    Latitude FLOAT,
    Longitude FLOAT,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DriverID) REFERENCES Drivers(DriverID)
);

CREATE TABLE Admins (
    AdminID INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100),
    Email VARCHAR(100),
    Password VARCHAR(100)
);

-- Users
INSERT INTO Users(Name,Phone,Email,Password,RegionID)
VALUES
('Nguyen Van A','0901111111','a@gmail.com','123',1),
('Tran Van B','0902222222','b@gmail.com','123',1);

-- Drivers
INSERT INTO Drivers(Name,Phone,Status,RegionID)
VALUES
('Driver 1','0911111111','Available',1),
('Driver 2','0922222222','Available',1);

-- Vehicles
INSERT INTO Vehicles(DriverID,PlateNumber,VehicleType)
VALUES
(1,'51A-12345','Bike'),
(2,'51B-67890','Car');

-- Trips
INSERT INTO Trips(UserID,DriverID,Status,Price)
VALUES
(1,1,'Completed',50000),
(2,2,'Requested',70000);

-- Payments
INSERT INTO Payments(TripID,Amount,Method,Status)
VALUES
(1,50000,'Cash','Paid');

-- Reviews
INSERT INTO Reviews(TripID,Rating,Comment)
VALUES
(1,5,'Good driver');

-- DriverLocations
INSERT INTO DriverLocations(DriverID,Latitude,Longitude)
VALUES
(1,21.03,105.85),
(2,21.02,105.84);

SELECT * FROM Trips;
SELECT * FROM Drivers;