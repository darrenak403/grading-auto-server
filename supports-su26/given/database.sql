IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'PE_PRN_26SU_11')
BEGIN
    CREATE DATABASE PE_PRN_26SU_11;
END
GO

USE PE_PRN_26SU_11;
GO

IF OBJECT_ID('PaperTopics', 'U') IS NOT NULL DROP TABLE PaperTopics;
IF OBJECT_ID('Topics', 'U') IS NOT NULL DROP TABLE Topics;
IF OBJECT_ID('PaperAuthors', 'U') IS NOT NULL DROP TABLE PaperAuthors;
IF OBJECT_ID('Papers', 'U') IS NOT NULL DROP TABLE Papers;
IF OBJECT_ID('Journals', 'U') IS NOT NULL DROP TABLE Journals;
IF OBJECT_ID('Researchers', 'U') IS NOT NULL DROP TABLE Researchers;
GO

CREATE TABLE Researchers (
    ResearcherID INT PRIMARY KEY IDENTITY(1,1),
    FullName NVARCHAR(100) NOT NULL,
    ResearchField NVARCHAR(200),
    JoinDate DATE
);

CREATE TABLE Journals (
    JournalID INT PRIMARY KEY IDENTITY(1,1),
    JournalName NVARCHAR(200) NOT NULL,
    ImpactFactor FLOAT 
);

CREATE TABLE Papers (
    PaperID INT PRIMARY KEY IDENTITY(1,1),
    Title NVARCHAR(300) NOT NULL,
    PublishYear INT,
    JournalID INT,
    CitationCount INT DEFAULT 0,
    FOREIGN KEY (JournalID) REFERENCES Journals(JournalID)
);

CREATE TABLE PaperAuthors (
    PaperID INT,
    ResearcherID INT,
    PRIMARY KEY (PaperID, ResearcherID),
    FOREIGN KEY (PaperID) REFERENCES Papers(PaperID),
    FOREIGN KEY (ResearcherID) REFERENCES Researchers(ResearcherID)
);

CREATE TABLE Topics (
    TopicID INT PRIMARY KEY IDENTITY(1,1),
    TopicName NVARCHAR(100) NOT NULL
);

CREATE TABLE PaperTopics (
    PaperID INT,
    TopicID INT,
    PRIMARY KEY (PaperID, TopicID),
    FOREIGN KEY (PaperID) REFERENCES Papers(PaperID),
    FOREIGN KEY (TopicID) REFERENCES Topics(TopicID)
);
GO

INSERT INTO Researchers (FullName, ResearchField, JoinDate) VALUES
('Dr. Vinh Quang Bui', 'Artificial Intelligence', '2016-03-14'),
('Ms. Lan Thi Ngo', 'Data Science', '2019-07-01'),
('Prof. Thanh Van Hoang', 'Biotechnology', '2011-09-18'),
('Dr. Manh Duc Vu', 'Renewable Energy', '2020-04-22'),
('Ms. Ngan Thi Kim Phan', 'Environmental Science', '2022-01-10'),
('Dr. Anh Hoang Le', 'Artificial Intelligence', '2018-11-05'),
('Assoc. Prof. Nam Van Nguyen', 'Artificial Intelligence', '2015-05-20'),
('Dr. Ha Thu Tran', 'Biotechnology', '2017-02-14'),
('Mr. Tuan Minh Dang', 'Renewable Energy', '2021-08-12');

INSERT INTO Journals (JournalName, ImpactFactor) VALUES
('Vietnam Journal of Computer Science', 2.4),
('International Journal of Biotechnology', 3.1),
('Journal of Renewable Energy', 1.8),
('Environmental Science Review', 2.0),
('IEEE Transactions on AI', 6.5);

INSERT INTO Papers (Title, PublishYear, JournalID, CitationCount) VALUES
('AI Applications in Medical Diagnosis', 2023, 1, 45),
('Big Data Systems for Online Education', 2022, 1, 20),
('Genetic Analysis of Cancer Inheritance', 2021, 2, 60),
('IoT Applications in Smart Grids', 2024, 3, 12),
('Assessing Water Pollution in the Mekong River', 2020, 4, 35),
('Deep Learning Models for Weather Forecasting', 2023, 1, 18),
('Research on Next-Generation Solar Cells', 2024, 3, 8),
('Microorganisms for Industrial Wastewater Treatment', 2022, 2, 27),
('Optimizing Neural Networks using Genetic Algorithms', 2025, 5, 50),
('Face Recognition under Low Light Conditions', 2024, 5, 32),
('Fake News Detection using Large Language Models', 2025, 1, 15),
('Extracting Natural Compounds from Herbs', 2023, 2, 42),
('Wind Power Output Prediction using Machine Learning', 2024, 3, 19);

INSERT INTO PaperAuthors (PaperID, ResearcherID) VALUES
(1, 1), (1, 2), (2, 2), (3, 3), (4, 4),
(4, 1), (5, 5), (5, 4), (6, 1), (6, 2),
(7, 4), (8, 3), (9, 6), (9, 7), (10, 1),
(10, 6), (11, 7), (12, 8), (12, 3), (13, 9), (13, 4);

INSERT INTO Topics (TopicName) VALUES
('Artificial Intelligence'),
('Big Data'),
('Biotechnology'),
('Renewable Energy'),
('Environment'),
('IoT');

INSERT INTO PaperTopics (PaperID, TopicID) VALUES
(1, 1), (1, 2), 
(2, 1), (2, 2), 
(3, 3), (3, 5),
(4, 6), (4, 4), 
(5, 5), 
(6, 1), (6, 2), 
(7, 4), 
(8, 3), (8, 5),
(9, 1), (10, 1), 
(11, 1), (11, 2),
(12, 3), 
(13, 4), (13, 1);
GO