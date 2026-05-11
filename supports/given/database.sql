-- 1. Tạo Database
CREATE DATABASE PE_PRN_26SP_11;
GO
USE PE_PRN_26SP_11;
GO

-- 2. Bang Giang vien (Instructors)
CREATE TABLE Instructors (
    InstructorID INT PRIMARY KEY IDENTITY(1,1),
    FullName NVARCHAR(100) NOT NULL,
    Expertise NVARCHAR(200), -- Chuyen mon
    HireDate DATE
);

-- 3. Bang Khoa hoc (Courses)
CREATE TABLE Courses (
    CourseID INT PRIMARY KEY IDENTITY(1,1),
    CourseName NVARCHAR(200) NOT NULL,
    Credits INT, -- So tin chi
    Department NVARCHAR(100)
);

-- 4. Bang Phan cong Giang day (Course Assignments)
-- Giai quyet quan he n-n giua Giang vien va Khoa hoc
CREATE TABLE CourseAssignments (
    CourseID INT,
    InstructorID INT,
    AssignmentDate DATE DEFAULT GETDATE(),
    PRIMARY KEY (CourseID, InstructorID),
    FOREIGN KEY (CourseID) REFERENCES Courses(CourseID),
    FOREIGN KEY (InstructorID) REFERENCES Instructors(InstructorID)
);

-- 5. Bang Lop hoc phan (Class Sections)
CREATE TABLE ClassSections (
    SectionID INT PRIMARY KEY IDENTITY(1,1),
    CourseID INT,
    RoomNumber NVARCHAR(20),
    Semester NVARCHAR(20),
    MaxCapacity INT,
    FOREIGN KEY (CourseID) REFERENCES Courses(CourseID)
);

-- 6. Bang Sinh vien (Students)
CREATE TABLE Students (
    StudentID INT PRIMARY KEY IDENTITY(1,1),
    StudentName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100),
    DateOfBirth DATE
);

-- 7. Bang Dang ky hoc (Enrollments)
CREATE TABLE Enrollments (
    EnrollmentID INT PRIMARY KEY IDENTITY(1,1),
    StudentID INT,
    SectionID INT,
    RegistrationDate DATE,
    Grade FLOAT, -- Diem so
    FOREIGN KEY (StudentID) REFERENCES Students(StudentID),
    FOREIGN KEY (SectionID) REFERENCES ClassSections(SectionID)
);
GO

-----------------------------------------------------------
--- CHEN DU LIEU MAU (Khong dau) ---
-----------------------------------------------------------

-- Instructors (5 records)
INSERT INTO Instructors (FullName, Expertise, HireDate) VALUES 
('TS. Tran Van Nam', 'Cong nghe phan mem', '2015-05-10'),
('ThS. Le Thi Binh', 'He thong thong tin', '2018-02-15'),
('GS. Nguyen Quoc Cuong', 'Tri tue nhan tao', '2010-11-20'),
('TS. Pham Minh Tuan', 'Mang may tinh', '2020-01-05'),
('ThS. Do Hong Hanh', 'An toan thong tin', '2021-09-12');

-- Courses (5 records)
INSERT INTO Courses (CourseName, Credits, Department) VALUES 
('Co so du lieu', 3, 'CNTT'),
('Lap trinh Web', 4, 'CNTT'),
('Toan roi rac', 3, 'Toan hoc'),
('Hoc may', 4, 'Khoa hoc du lieu'),
('Mang may tinh co ban', 3, 'Vien thong');

-- CourseAssignments (10 records)
INSERT INTO CourseAssignments (CourseID, InstructorID) VALUES 
(1, 1), (1, 2), (2, 2), (2, 4), (3, 3), 
(4, 3), (4, 1), (5, 4), (5, 5), (3, 5);

-- ClassSections (5 records)
INSERT INTO ClassSections (CourseID, RoomNumber, Semester, MaxCapacity) VALUES 
(1, 'A1-201', 'Hoc ky 1 2024', 50),
(2, 'B2-105', 'Hoc ky 1 2024', 40),
(3, 'C3-303', 'Hoc ky 2 2024', 60),
(4, 'Lab-01', 'Hoc ky 2 2024', 30),
(5, 'A1-402', 'Hoc ky 1 2024', 45);

-- Students (5 records)
INSERT INTO Students (StudentName, Email, DateOfBirth) VALUES 
('Nguyen Hoang Long', 'longnh@student.edu.vn', '2004-05-12'),
('Vu Minh Anh', 'anhvm@student.edu.vn', '2004-08-20'),
('Le Bao Chau', 'chaulb@student.edu.vn', '2003-12-01'),
('Dang Tien Dat', 'datdt@student.edu.vn', '2004-01-15'),
('Tran Thu Ha', 'hatt@student.edu.vn', '2004-10-30');

-- Enrollments (10 records)
INSERT INTO Enrollments (StudentID, SectionID, RegistrationDate, Grade) VALUES 
(1, 1, '2024-01-02', 8.5),
(2, 1, '2024-01-02', 7.0),
(3, 2, '2024-01-05', 9.0),
(4, 3, '2024-02-10', NULL),
(5, 4, '2024-02-12', 6.5),
(1, 5, '2024-01-03', 8.0),
(2, 2, '2024-01-06', NULL),
(3, 4, '2024-02-15', NULL),
(4, 1, '2024-01-04', 7.5),
(5, 5, '2024-01-05', 9.5);
GO
