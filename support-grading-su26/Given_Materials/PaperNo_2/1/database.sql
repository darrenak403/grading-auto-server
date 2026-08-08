-- 1. Tao co so du lieu
CREATE DATABASE PRN232_PE_SU26_13;
GO

USE PRN232_PE_SU26_13;
GO

-- 2. Bang Goi tap 
CREATE TABLE MembershipPackages (
    PackageID INT IDENTITY PRIMARY KEY,
    PackageName NVARCHAR(100), -- VD: Goi Thang, Goi Nam, Goi VIP
    Price DECIMAL(10,2)
);

-- 3. Bang Hoi vien 
CREATE TABLE Members (
    MemberID INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(100),
    PackageID INT FOREIGN KEY REFERENCES MembershipPackages(PackageID),
    JoinDate DATE
);

-- 4. Bang Huan luyen vien 
CREATE TABLE Trainers (
    TrainerID INT IDENTITY PRIMARY KEY,
    TrainerName NVARCHAR(100),
    Email NVARCHAR(100),
    ExperienceYears INT
);

-- 5. Bang Chuyen mon/Chung chi 
CREATE TABLE Specializations (
    SpecID INT IDENTITY PRIMARY KEY,
    SpecName NVARCHAR(100), -- VD: Yoga, Cardio, The hinh, Chinh dang
    Description NVARCHAR(250)
);

-- 6. Bang Lich hen tap 
CREATE TABLE Bookings (
    BookingID INT IDENTITY PRIMARY KEY,
    TrainerID INT FOREIGN KEY REFERENCES Trainers(TrainerID),
    BookingDate DATETIME,
    SessionTime DATETIME
);

-- 7. Bang Chi tiet lich hen - Hoi vien (n-n giua Booking va Member)
CREATE TABLE BookingDetails (
    BookingID INT,
    MemberID INT,
    DurationMinutes INT, -- Thoi gian tap (phut)
    Status NVARCHAR(50), -- VD: Da tap, Da huy, Cho mien
    PRIMARY KEY (BookingID, MemberID),
    FOREIGN KEY (BookingID) REFERENCES Bookings(BookingID),
    FOREIGN KEY (MemberID) REFERENCES Members(MemberID)
);

-- 8. Bang Chuyen mon cua Huan luyen vien (n-n giua Trainer va Specialization)
CREATE TABLE TrainerSpecs (
    TrainerID INT,
    SpecID INT,
    PRIMARY KEY (TrainerID, SpecID),
    FOREIGN KEY (TrainerID) REFERENCES Trainers(TrainerID),
    FOREIGN KEY (SpecID) REFERENCES Specializations(SpecID)
);

-- =============================================
-- DU LIEU MAU 
-- =============================================

-- Insert cho MembershipPackages (5 ban ghi)
INSERT INTO MembershipPackages (PackageName, Price) VALUES 
(N'Goi Thang Co Ban', 300000.00), 
(N'Goi Binh Minh', 450000.00), 
(N'Goi Chuyen Nghiep', 800000.00),
(N'Goi Gia Dinh', 1500000.00),
(N'Goi VIP Tron Goi', 3000000.00),
(N'Goi VVIP Kim Cuong', 6000000.00);

-- Insert cho Members (5 ban ghi)
INSERT INTO Members (FullName, PackageID, JoinDate) VALUES 
(N'Cao Van Nam', 1, '2026-01-10'), 
(N'Hoang Thuy Linh', 2, '2026-02-15'),
(N'Do Minh Quan', 3, '2026-03-01'), 
(N'Phan Thanh Thao', 4, '2026-03-12'),
(N'Bui Tien Dung', 5, '2026-04-05');

-- Insert cho Trainers (5 ban ghi)
INSERT INTO Trainers (TrainerName, Email, ExperienceYears) VALUES 
(N'HLV Nguyen Hoan', N'hoan.nguyen@gym.com', 5),
(N'HLV Tran Thao', N'thao.tran@gym.com', 3),
(N'HLV Le Hai', N'hai.le@gym.com', 7),
(N'HLV Pham Huy', N'huy.pham@gym.com', 2),
(N'HLV Vu Hoang', N'hoang.vu@gym.com', 4),
(N'HLV Nguyen Manh', N'manh.nguyen@gym.com', 6);

-- Insert cho Specializations (5 ban ghi)
INSERT INTO Specializations (SpecName, Description) VALUES 
(N'Giam can nhanh', N'Bai tap cuong do cao tieu hao calo'), 
(N'Tang co bap', N'Tap ta va dinh duong the hinh chuyen sau'), 
(N'Yoga va Thien', N'Can bang tam tri va nang cao do deo dai'), 
(N'Boxing va Vo thuat', N'Tap luyen phan xa va tu ve'),
(N'Phuc hoi chuc nang', N'Ho tro sau chan thuong xuong khop');

-- Insert cho Bookings (5 ban ghi)
INSERT INTO Bookings (TrainerID, BookingDate, SessionTime) VALUES 
(1, '2026-06-25 08:00:00', '2026-06-26 09:00:00'),
(2, '2026-06-25 09:30:00', '2026-06-26 14:00:00'),
(3, '2026-06-26 10:00:00', '2026-06-27 16:30:00'),
(4, '2026-06-26 15:00:00', '2026-06-27 08:00:00'),
(5, '2026-06-27 11:15:00', '2026-06-28 19:00:00');

-- Insert cho BookingDetails (5 ban ghi)
INSERT INTO BookingDetails (BookingID, MemberID, DurationMinutes, Status) VALUES 
(1, 1, 60, N'Da tap'), 
(2, 2, 90, N'Da tap'), 
(3, 3, 60, N'Cho mien'), 
(4, 4, 45, N'Da huy'), 
(5, 5, 120, N'Da tap');

-- Insert cho TrainerSpecs (6 ban ghi - n-n)
INSERT INTO TrainerSpecs (TrainerID, SpecID) VALUES 
(1, 1), (1, 2), -- HLV Nguyen Manh: Giam can va Tang co
(2, 3),         -- HLV Tran Thao: Chuyen Yoga
(3, 2), (3, 4), -- HLV Le Hai: Tang co va Boxing
(4, 1),         -- HLV Pham Huy: Giam can
(5, 5);         -- HLV Vu Hoang: Phuc hoi