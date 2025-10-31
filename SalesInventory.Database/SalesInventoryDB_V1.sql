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
GO
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
GO
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
GO
-- 4. Sales Orders Table
CREATE TABLE SalesOrders (
	OrderId INT PRIMARY KEY IDENTITY(1,1),
	OrderNumber NVARCHAR(50) NOT NULL UNIQUE,
	CustomerId INT NOT NULL, 
	OrderDate DATETIME NOT NULL DEFAULT GETDATE(), 
	TotalAmount DECIMAL(18,2) NOT NULL,
	Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Status can be Pending, Completed, Cancelled
	CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
	FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)	
);
GO
-- 5. Sales Order Details Table
CREATE TABLE SalesOrderDetails (
	OrderDetailsId INT PRIMARY KEY IDENTITY(1,1),
	OrderId INT NOT NULL, 
	ProductId INT NOT NULL,
	Quantity INT NOT NULL,
	UnitPrice DECIMAL(18,2) NOT NULL,
	SubTotal DECIMAL(18,2) NOT NULL,
	FOREIGN KEY (OrderId) REFERENCES SalesOrders(OrderId),
	FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);

GO

-- Sample Data Insertion (Small dataset for V1)

-- Insert Categories
INSERT INTO Categories (CategoryName, Description) VALUES
('Electronics', 'Electronic items and gadgets'),
('Clothing', 'Apparel and fashion items'),
('Food & Beverages' , 'Food and drink products'),
('Furniture', 'Home and office furniture'),
('Books', 'Books and publications');
GO

SELECT * FROM Categories
GO

-- Insert Products (50 products)

DECLARE @i INT=1;
WHILE @i <=50
BEGIN
	INSERT INTO Products (ProductName, CategoryId, UnitPrice, StockQuantity, ReorderLevel, CreatedDate)
	VALUES (
		'Product' + CAST(@i AS NVARCHAR),
		((@i -1 )%5) +1, -- 50 products distributed equally to 5 categories
		-- if @i =2 then 2 -1 = 1, 1 /5 = 0, 1 still remains so 1%5 =1, then 1+1
		ROUND(RAND() * 1000 +10, 2),
		CAST(RAND() * 500 AS INT),
		10,
		DATEADD(DAY, - RAND() * 365, GETDATE())
	);
	SET @i = @i+1;
END
GO

-- Insert Customer (100 Customers)
DECLARE @i INT =1;
WHILE @i <=100
BEGIN
	INSERT INTO Customers (CustomerName, Email, Phone, City, Country, CreatedDate)
	VALUES (
		'Customer' + CAST(@i AS NVARCHAR),
		'Customer' + CAST(@i AS NVARCHAR) + '@gmail.com',
		'+88017' + RIGHT('0000000' +CAST(@i AS NVARCHAR), 8),
		CASE(@I % 5)
			WHEN 0 THEN 'Dhaka'
			WHEN 1 THEN 'Chittagong'
			WHEN 2 THEN 'Rajshahi'
			WHEN 3 THEN 'Rangpur'
			ELSE 'Barishal'
		END,
		'Bangladesh',
		DATEADD(DAY, -RAND() * 730, GETDATE())
	);
	SET @i = @i +1;
END

GO

SELECT * FROM Customers
GO

-- Insert Sales Orders (500 Orders for peformance testing)
DECLARE @i INT =1;
WHILE @i <= 500
BEGIN
    DECLARE @CustomerId INT = (RAND() * 100) + 1;
    DECLARE @OrderDate DATETIME = DATEADD(DAY, -RAND() * 365, GETDATE());
    DECLARE @OrderNumber NVARCHAR(50) = 'ORD-' + FORMAT(@i, '000000');
    
    INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, TotalAmount, Status, CreatedDate)
    VALUES (
        @OrderNumber,
        @CustomerId,
        @OrderDate,
        0, -- Will be updated after details
        CASE (CAST(RAND() * 3 AS INT))
            WHEN 0 THEN 'Pending'
            WHEN 1 THEN 'Completed'
            ELSE 'Cancelled'
        END,
        @OrderDate
    );
    
    DECLARE @OrderId INT = SCOPE_IDENTITY();
    DECLARE @TotalAmount DECIMAL(18,2) = 0;
    
    -- Insert 1-5 order details per order
    DECLARE @DetailCount INT = (RAND() * 5) + 1;
    DECLARE @j INT = 1;
    WHILE @j <= @DetailCount
    BEGIN
        DECLARE @ProductId INT = (RAND() * 50) + 1;
        DECLARE @Quantity INT = (RAND() * 10) + 1;
        DECLARE @UnitPrice DECIMAL(18,2) = (SELECT UnitPrice FROM Products WHERE ProductId = @ProductId);
        DECLARE @SubTotal DECIMAL(18,2) = @UnitPrice * @Quantity;
        
        INSERT INTO SalesOrderDetails (OrderId, ProductId, Quantity, UnitPrice, SubTotal)
        VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice, @SubTotal);
        
        SET @TotalAmount = @TotalAmount + @SubTotal;
        SET @j = @j + 1;
    END
    
    -- Update order total
    UPDATE SalesOrders SET TotalAmount = @TotalAmount WHERE OrderId = @OrderId;
    
    SET @i = @i + 1;
END

GO



-- This shows all FK relationships.
-- You can then see which tables have no references
SELECT 
    fk.name AS ForeignKeyName,
    tp.name AS ParentTable,
    tr.name AS ReferencedTable
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables AS tr ON fk.referenced_object_id = tr.object_id
ORDER BY tr.name;


-- Verification Queries
SELECT 'Categories' AS TableName, COUNT(*) AS RecordCount FROM Categories
UNION ALL
SELECT 'Products', COUNT(*) FROM Products
UNION ALL
SELECT 'Customers', COUNT(*) FROM Customers
UNION ALL
SELECT 'SalesOrders', COUNT(*) FROM SalesOrders
UNION ALL
SELECT 'SalesOrderDetails', COUNT(*) FROM SalesOrderDetails;