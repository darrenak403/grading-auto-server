-- 1. Tao co so du lieu

CREATE DATABASE PRN232_PE_SU26_11;

GO



USE PRN232_PE_SU26_11;

GO



-- 2. Bang The loai phim 

CREATE TABLE Genres (

    GenreID INT IDENTITY PRIMARY KEY,

    GenreName NVARCHAR(100), -- VD: Hanh dong, Kinh di, Hoat hinh

    Description NVARCHAR(250)

);



-- 3. Bang Phim 

CREATE TABLE Movies (

    MovieID INT IDENTITY PRIMARY KEY,

    Title NVARCHAR(100),

    Duration INT, -- Thoi luong (phut)

    BasePrice DECIMAL(10,2) -- Gia ve goc

);



-- 4. Bang Khan gia 

CREATE TABLE Viewers (

    ViewerID INT IDENTITY PRIMARY KEY,

    FullName NVARCHAR(100),

    Email NVARCHAR(100),

    Age INT

);



-- 5. Bang Quat do an kem 

CREATE TABLE Combos (

    ComboID INT IDENTITY PRIMARY KEY,

    ComboName NVARCHAR(100), -- VD: Bap rang bo, Nuoc ngot

    Price DECIMAL(10,2)

);



-- 6. Bang Ve xem phim 

CREATE TABLE Tickets (

    TicketID INT IDENTITY PRIMARY KEY,

    ViewerID INT FOREIGN KEY REFERENCES Viewers(ViewerID),

    BookingDate DATETIME,

    ShowTime DATETIME

);



-- 7. Bang Chi tiet ve - phim (n-n giua Ticket va Movie)

CREATE TABLE TicketDetails (

    TicketID INT,

    MovieID INT,

    SeatNumber NVARCHAR(10), -- So ghe: A1, B2...

    Quantity INT,

    PRIMARY KEY (TicketID, MovieID),

    FOREIGN KEY (TicketID) REFERENCES Tickets(TicketID),

    FOREIGN KEY (MovieID) REFERENCES Movies(MovieID)

);



-- 8. Bang Chi tiet the loai cua phim (n-n giua Movie va Genre)

CREATE TABLE MovieGenres (

    MovieID INT,

    GenreID INT,

    PRIMARY KEY (MovieID, GenreID),

    FOREIGN KEY (MovieID) REFERENCES Movies(MovieID),

    FOREIGN KEY (GenreID) REFERENCES Genres(GenreID)

);



-- =============================================

-- DU LIEU MAU

-- =============================================



-- Insert cho Genres (6 ban ghi)

INSERT INTO Genres (GenreName, Description) VALUES 

(N'Hanh dong', N'Phim co nhieu canh duoi bat, danh nhau gay can'), 

(N'Kinh di', N'Phim co yeu to ma mi, giat gan, dang so'), 

(N'Hoat hinh', N'Phim danh cho thieu nhi va gia dinh'),

(N'Hai huoc', N'Phim mang lai tieng cuoi va giai tri'),

(N'Tinh cam', N'Phim tap trung vao cau chuyen tinh yeu'),

(N'Khoa hoc', N'Phim nghien cuu khoa hoc, vien tuong');



-- Insert cho Movies (6 ban ghi)

INSERT INTO Movies (Title, Duration, BasePrice) VALUES 

(N'Lat Mat 7', 120, 90000.00), 

(N'Ngay Xua Co Mot Chuyen Tinh', 110, 85000.00),

(N'Biet Doi Anh Hung', 150, 120000.00), 

(N'Tham Tu Connan', 105, 80000.00),

(N'Quy Cau', 95, 95000.00),

(N'Pham Nhan Tu Tien', 90, 90000.00);



-- Insert cho Viewers (5 ban ghi)

INSERT INTO Viewers (FullName, Email, Age) VALUES 

(N'Nguyen Ngoc Hoan', N'hoan.nguyen@gmail.com', 20),

(N'Tran Thi Mai', N'mai.tran@gmail.com', 17),

(N'Le Hoang Long', N'long.le@gmail.com', 25),

(N'Pham Hong Nhung', N'nhung.pham@gmail.com', 15),

(N'Vu Minh Tri', N'tri.vu@gmail.com', 30);



-- Insert cho Combos (5 ban ghi)

INSERT INTO Combos (ComboName, Price) VALUES 

(N'Bap Rang Bo Single', 45000.00), 

(N'Nuoc Ngot Lon', 30000.00), 

(N'Combo Doi Bap Nuoc', 95000.00), 

(N'Kho Ga Xe Cay', 35000.00),

(N'Xuc Xich Nuong', 25000.00);



-- Insert cho Tickets (5 ban ghi)

INSERT INTO Tickets (ViewerID, BookingDate, ShowTime) VALUES 

(1, '2026-06-20 14:00:00', '2026-06-21 19:30:00'),

(2, '2026-06-20 15:30:00', '2026-06-21 21:00:00'),

(3, '2026-06-21 09:00:00', '2026-06-22 18:00:00'),

(4, '2026-06-21 10:15:00', '2026-06-22 14:00:00'),

(5, '2026-06-22 08:00:00', '2026-06-22 20:30:00');



-- Insert cho TicketDetails (5 ban ghi)

INSERT INTO TicketDetails (TicketID, MovieID, SeatNumber, Quantity) VALUES 

(1, 1, N'H12', 2), 

(2, 2, N'A05', 1), 

(3, 3, N'F09', 3), 

(4, 4, N'B01', 1), 

(5, 5, N'G11', 2);



-- Insert cho MovieGenres (6 ban ghi - n-n)

INSERT INTO MovieGenres (MovieID, GenreID) VALUES 

(1, 1), (1, 4), -- Lat Mat 7: Vua Hanh dong vua Hai huoc

(2, 5),         -- Ngay Xua Co Mot Chuyen Tinh: Tinh cam

(3, 1),         -- Biet Doi Anh Hung: Hanh dong

(4, 3), (4, 4), -- Connan: Hoat hinh, Hai huoc

(5, 2);         -- Quy Cau: Kinh di