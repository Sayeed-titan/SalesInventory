-- Version 1: Database Schema WITHOUT Indexes
-- Database: SalesInventoryDB_V1

USE master;
GO

SELECT * FROM sys.databases
GO

-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SalesInventoryDB_V1')
BEGIN
	CREATE DATABASE SalesInventoryDB_V1;
END
GO

USE SalesInventoryDB_V1;
GO

-- 1. Categories Table
CREATE TABLE Categories(
	CategoryId INT PRIMARY KEY IDENTITY(1,1),
	CategoryName NVARCHAR(100) NOT NULL,
	Description NVARCHAR(500),
	CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);