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

-- 2. Products Table
CREATE TABLE Products(
	ProductId INT PRIMARY KEY IDENTITY(1,1),
	ProductName NVARCHAR(200) NOT NULL, 
	-- Column Level Constraints : REFERENCES Categories(CategoryId)
	CategoryId INT NOT NULL,
	UnitPrice DECIMAL(18,2) NOT NULL,
	StockQuantity INT NOT NULL DEFAULT 0,
	ReorderLevel INT NOT NULL DEFAULT 10,
	IsActive BIT NOT NULL DEFAULT 1,
	CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
	-- Add Foreign Key for CategoryId coming from Category Table
	-- Table Level Constraints
	FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId)
);

-- 3. Customers Table
CREATE TABLE Customers (
	CustomerId INT PRIMARY KEY IDENTITY (1,1),
	CustomerName NVARCHAR(200) NOT NULL, 
	Email NVARCHAR(100), 
	Phone NVARCHAR(20),
	Address NVARCHAR(500),
	City NVARCHAR(100),
	Country NVARCHAR(100),
	CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);