-- =====================================================
-- SALES INVENTORY DATABASE - LARGE DATASET
-- Version 1: NO INDEXES (for performance testing)
-- Dataset: 10,000 orders + 30,000+ order details
-- =====================================================

USE master;
GO

-- Drop database if exists (for clean start)
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'SalesInventoryDB_V1')
BEGIN
    ALTER DATABASE SalesInventoryDB_V1 SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE SalesInventoryDB_V1;
    PRINT '✓ Old database dropped';
END
GO

-- Create new database
CREATE DATABASE SalesInventoryDB_V1;
GO

USE SalesInventoryDB_V1;
GO

-- 'Creating Tables...'

-- 1. Categories Table
CREATE TABLE Categories (
    CategoryId INT PRIMARY KEY IDENTITY(1,1),
    CategoryName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 2. Products Table (NO INDEXES!)
CREATE TABLE Products (
    ProductId INT PRIMARY KEY IDENTITY(1,1),
    ProductName NVARCHAR(200) NOT NULL,
    CategoryId INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL DEFAULT 0,
    ReorderLevel INT NOT NULL DEFAULT 10,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId)
    -- DELIBERATELY NO INDEX on CategoryId
    -- DELIBERATELY NO INDEX on ProductName
    -- DELIBERATELY NO INDEX on CreatedDate
);
GO

-- 3. Customers Table
CREATE TABLE Customers (
    CustomerId INT PRIMARY KEY IDENTITY(1,1),
    CustomerName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(100),
    Phone NVARCHAR(20),
    Address NVARCHAR(500),
    City NVARCHAR(100),
    Country NVARCHAR(100),
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
    -- DELIBERATELY NO INDEX on CustomerName
);
GO

-- 4. Sales Orders Table (NO INDEXES on critical columns!)
CREATE TABLE SalesOrders (
    OrderId INT PRIMARY KEY IDENTITY(1,1),
    OrderNumber NVARCHAR(50) NOT NULL UNIQUE,
    CustomerId INT NOT NULL,
    OrderDate DATETIME NOT NULL DEFAULT GETDATE(),
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);
PRINT '✓ SalesOrders table created (NO INDEXES - SLOW!)';

-- 5. Sales Order Details Table
CREATE TABLE SalesOrderDetails (
    OrderDetailId INT PRIMARY KEY IDENTITY(1,1),
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    SubTotal DECIMAL(18,2) NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES SalesOrders(OrderId),
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
    -- DELIBERATELY NO INDEX on OrderId
    -- DELIBERATELY NO INDEX on ProductId
);
GO



-- =====================================================
-- INSERT DATA
-- =====================================================

-- 1. Insert Categories (10 categories)
INSERT INTO Categories (CategoryName, Description) VALUES
('Electronics', 'Electronic items and gadgets'),
('Clothing', 'Apparel and fashion items'),
('Food & Beverages', 'Food and drink products'),
('Furniture', 'Home and office furniture'),
('Books', 'Books and publications'),
('Sports & Outdoors', 'Sports equipment and outdoor gear'),
('Toys & Games', 'Toys, games, and hobbies'),
('Health & Beauty', 'Health and beauty products'),
('Automotive', 'Car parts and accessories'),
('Home & Garden', 'Home improvement and gardening');
GO

-- 2. Insert Products (500 products for variety)
SET NOCOUNT ON;

DECLARE @i INT = 1;
DECLARE @ProductName NVARCHAR(200);
DECLARE @CategoryId INT;
DECLARE @UnitPrice DECIMAL(18,2);
DECLARE @StockQty INT;
DECLARE @CreatedDate DATETIME;

WHILE @i <= 500
BEGIN
    SET @ProductName = 'Product ' + RIGHT('0000' + CAST(@i AS NVARCHAR), 4);
    SET @CategoryId = ((@i - 1) % 10) + 1;
    SET @UnitPrice = ROUND(RAND(CHECKSUM(NEWID())) * 990 + 10, 2); -- $10 to $1000
    SET @StockQty = CAST(RAND(CHECKSUM(NEWID())) * 1000 AS INT); -- 0 to 1000
    SET @CreatedDate = DATEADD(DAY, -RAND(CHECKSUM(NEWID())) * 730, GETDATE()); -- Last 2 years
    
    INSERT INTO Products (ProductName, CategoryId, UnitPrice, StockQuantity, ReorderLevel, IsActive, CreatedDate)
    VALUES (@ProductName, @CategoryId, @UnitPrice, @StockQty, 20, 1, @CreatedDate);
    
    IF @i % 100 = 0
        PRINT '  ✓ ' + CAST(@i AS NVARCHAR) + ' products inserted...';
    
    SET @i = @i + 1;
END

-- 3. Insert Customers (2,000 customers)
SET @i = 1;
DECLARE @CustomerName NVARCHAR(200);
DECLARE @Email NVARCHAR(100);
DECLARE @Phone NVARCHAR(20);
DECLARE @City NVARCHAR(100);

WHILE @i <= 2000
BEGIN
    SET @CustomerName = 'Customer ' + RIGHT('00000' + CAST(@i AS NVARCHAR), 5);
    SET @Email = 'customer' + CAST(@i AS NVARCHAR) + '@example.com';
    SET @Phone = '+1-555-' + RIGHT('0000' + CAST(@i AS NVARCHAR), 4);
    SET @City = CASE (CAST(RAND(CHECKSUM(NEWID())) * 10 AS INT))
        WHEN 0 THEN 'New York'
        WHEN 1 THEN 'Los Angeles'
        WHEN 2 THEN 'Chicago'
        WHEN 3 THEN 'Houston'
        WHEN 4 THEN 'Phoenix'
        WHEN 5 THEN 'Philadelphia'
        WHEN 6 THEN 'San Antonio'
        WHEN 7 THEN 'San Diego'
        WHEN 8 THEN 'Dallas'
        ELSE 'San Jose'
    END;
    SET @CreatedDate = DATEADD(DAY, -RAND(CHECKSUM(NEWID())) * 1095, GETDATE()); -- Last 3 years
    
    INSERT INTO Customers (CustomerName, Email, Phone, City, Country, Address, CreatedDate)
    VALUES (@CustomerName, @Email, @Phone, @City, 'USA', '123 Main St', @CreatedDate);
    
    IF @i % 500 = 0
        PRINT '  ✓ ' + CAST(@i AS NVARCHAR) + ' customers inserted...';
    
    SET @i = @i + 1;
END


-- 4. Insert Sales Orders (10,000 orders - THIS WILL MAKE IT SLOW!)
SET @i = 1;
DECLARE @OrderNumber NVARCHAR(50);
DECLARE @CustomerId INT;
DECLARE @OrderDate DATETIME;
DECLARE @Status NVARCHAR(50);
DECLARE @OrderId INT;
DECLARE @TotalAmount DECIMAL(18,2);

WHILE @i <= 10000
BEGIN
    SET @OrderNumber = 'ORD-' + RIGHT('000000' + CAST(@i AS NVARCHAR), 6);
    SET @CustomerId = CAST(RAND(CHECKSUM(NEWID())) * 1999 AS INT) + 1; -- Random customer 1-2000
    SET @OrderDate = DATEADD(DAY, -RAND(CHECKSUM(NEWID())) * 730, GETDATE()); -- Last 2 years
    SET @Status = CASE (CAST(RAND(CHECKSUM(NEWID())) * 10 AS INT))
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Pending'
        WHEN 2 THEN 'Pending'
        WHEN 3 THEN 'Completed'
        WHEN 4 THEN 'Completed'
        WHEN 5 THEN 'Completed'
        WHEN 6 THEN 'Completed'
        WHEN 7 THEN 'Completed'
        WHEN 8 THEN 'Completed'
        ELSE 'Cancelled'
    END;
    
    INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, TotalAmount, Status, CreatedDate)
    VALUES (@OrderNumber, @CustomerId, @OrderDate, 0, @Status, @OrderDate);
    
    SET @OrderId = SCOPE_IDENTITY();
    SET @TotalAmount = 0;
    
    -- Insert 2-5 order details per order
    DECLARE @DetailCount INT = CAST(RAND(CHECKSUM(NEWID())) * 4 AS INT) + 2; -- 2 to 5 items
    DECLARE @j INT = 1;
    DECLARE @ProductId INT;
    DECLARE @Quantity INT;
    DECLARE @DetailUnitPrice DECIMAL(18,2);
    DECLARE @SubTotal DECIMAL(18,2);
    
    WHILE @j <= @DetailCount
    BEGIN
        SET @ProductId = CAST(RAND(CHECKSUM(NEWID())) * 499 AS INT) + 1; -- Random product 1-500
        SET @Quantity = CAST(RAND(CHECKSUM(NEWID())) * 10 AS INT) + 1; -- 1 to 10 qty
        SET @DetailUnitPrice = (SELECT UnitPrice FROM Products WHERE ProductId = @ProductId);
        SET @SubTotal = @DetailUnitPrice * @Quantity;
        
        INSERT INTO SalesOrderDetails (OrderId, ProductId, Quantity, UnitPrice, SubTotal)
        VALUES (@OrderId, @ProductId, @Quantity, @DetailUnitPrice, @SubTotal);
        
        SET @TotalAmount = @TotalAmount + @SubTotal;
        SET @j = @j + 1;
    END
    
    -- Update order total
    UPDATE SalesOrders SET TotalAmount = @TotalAmount WHERE OrderId = @OrderId;
    
    IF @i % 1000 = 0
        PRINT '  ✓ ' + CAST(@i AS NVARCHAR) + ' orders inserted...';
    
    SET @i = @i + 1;
END

SET NOCOUNT OFF;
GO

-- =====================================================
-- VERIFY DATA
-- =====================================================


SELECT 
    'Categories' AS TableName, 
    COUNT(*) AS RecordCount,
    CAST(SUM(CAST(DATALENGTH(*) AS BIGINT)) / 1024.0 / 1024.0 AS DECIMAL(10,2)) AS SizeMB
FROM Categories
UNION ALL
SELECT 'Products', COUNT(*), CAST(SUM(CAST(DATALENGTH(*) AS BIGINT)) / 1024.0 / 1024.0 AS DECIMAL(10,2))
FROM Products
UNION ALL
SELECT 'Customers', COUNT(*), CAST(SUM(CAST(DATALENGTH(*) AS BIGINT)) / 1024.0 / 1024.0 AS DECIMAL(10,2))
FROM Customers
UNION ALL
SELECT 'SalesOrders', COUNT(*), CAST(SUM(CAST(DATALENGTH(*) AS BIGINT)) / 1024.0 / 1024.0 AS DECIMAL(10,2))
FROM SalesOrders
UNION ALL
SELECT 'SalesOrderDetails', COUNT(*), CAST(SUM(CAST(DATALENGTH(*) AS BIGINT)) / 1024.0 / 1024.0 AS DECIMAL(10,2))
FROM SalesOrderDetails;
GO

-- Test query to verify data distribution
PRINT 'Sample Order Date Distribution:';
SELECT 
    YEAR(OrderDate) AS OrderYear,
    MONTH(OrderDate) AS OrderMonth,
    COUNT(*) AS OrderCount
FROM SalesOrders
GROUP BY YEAR(OrderDate), MONTH(OrderDate)
ORDER BY OrderYear DESC, OrderMonth DESC;
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