-- 1. Database Setup
CREATE DATABASE IF NOT EXISTS pocketree;
USE pocketree;

-- 2. Parent Tables (Must be created first)

-- Table: Levels (Referenced by Users and UserVouchers)
CREATE TABLE IF NOT EXISTS Levels (
    LevelID INT AUTO_INCREMENT PRIMARY KEY,
    LevelName VARCHAR(20) NOT NULL, -- e.g., Seedling, Sapling
    MinCoins INT NOT NULL,
    LevelImageURL VARCHAR(255)
);

-- Table: Users (Referenced by History, Skins, Vouchers)
CREATE TABLE IF NOT EXISTS Users (
    UserID INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    TotalCoins INT DEFAULT 0,
    CurrentLevelID INT,
    LastLoginDate DATETIME,
    FOREIGN KEY (CurrentLevelID) REFERENCES Levels(LevelID)
);

-- Table: Tasks (Referenced by UserTaskHistory)
CREATE TABLE IF NOT EXISTS Tasks (
    TaskID INT AUTO_INCREMENT PRIMARY KEY,
    Description TEXT NOT NULL,
    Difficulty ENUM('Easy', 'Normal', 'Hard') NOT NULL,
    CoinReward INT NOT NULL,
    RequiresEvidence BOOLEAN DEFAULT FALSE
);

-- Table: Skins (Referenced by UserSkins)
CREATE TABLE IF NOT EXISTS Skins (
    SkinID INT AUTO_INCREMENT PRIMARY KEY,
    SkinName VARCHAR(50) NOT NULL,
    ImageURL VARCHAR(255)
);

-- Table: Vouchers (Referenced by UserVouchers)
CREATE TABLE IF NOT EXISTS Vouchers (
    VoucherID INT AUTO_INCREMENT PRIMARY KEY,
    VoucherName VARCHAR(50) NOT NULL,
    Description TEXT
);

-- Table: Badges (Standalone)
CREATE TABLE IF NOT EXISTS Badges (
    BadgeID INT AUTO_INCREMENT PRIMARY KEY,
    BadgeName VARCHAR(50) NOT NULL,
    CriteriaType VARCHAR(20), -- e.g., 'TaskCount'
    RequiredDifficulty VARCHAR(10), -- e.g., 'Hard'
    RequiredCount INT
);

-- Table: GlobalMissions (Standalone)
CREATE TABLE IF NOT EXISTS GlobalMissions (
    MissionID INT AUTO_INCREMENT PRIMARY KEY,
    MissionName VARCHAR(100) NOT NULL,
    TotalRequiredTrees INT NOT NULL,
    CurrentTreeCount INT DEFAULT 0
);

-- 3. Child Tables (Contain Foreign Keys)

-- Table: UserTaskHistory
CREATE TABLE IF NOT EXISTS UserTaskHistory (
    HistoryID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT NOT NULL,
    TaskID INT NOT NULL,
    Status VARCHAR(20) DEFAULT 'Completed',
    CompletionDate DATETIME,
    FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
    FOREIGN KEY (TaskID) REFERENCES Tasks(TaskID)
);

-- Table: UserSkins
CREATE TABLE IF NOT EXISTS UserSkins (
    UserSkinID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT NOT NULL,
    SkinID INT NOT NULL,
    RedemptionDate DATETIME,
    IsEquipped BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
    FOREIGN KEY (SkinID) REFERENCES Skins(SkinID)
);

-- Table: UserVouchers
CREATE TABLE IF NOT EXISTS UserVouchers (
    UserVoucherID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT NOT NULL,
    VoucherID INT NOT NULL,
    LevelReachedID INT, -- The level that unlocked this voucher
    RedemptionCode VARCHAR(20),
    FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
    FOREIGN KEY (VoucherID) REFERENCES Vouchers(VoucherID),
    FOREIGN KEY (LevelReachedID) REFERENCES Levels(LevelID)
);

-- Optional: Seed Data (Example Levels based on PDF)
INSERT INTO Levels (LevelName, MinCoins, LevelImageURL) VALUES 
('Seedling', 0, '/images/seedling.png'),
('Sapling', 250, '/images/sapling.png'),
('Mighty Oak', 500, '/images/oak.png');