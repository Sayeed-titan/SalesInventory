-- Version 1: Database Schema WITHOUT Indexes
-- Database: SalesInventoryDB_V1

USE master;
GO

-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SalesInventoryDB_V1')
BEGIN
	CREATE DATABASE SalesInventoryDB_V1;
END
GO

USE SalesInventoryDB_V1;
GO

