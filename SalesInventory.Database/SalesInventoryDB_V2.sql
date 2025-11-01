
USE master;
GO


-- Backup V1 first for comparison
BACKUP DATABASE SalesInventoryDB_V1
TO DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\SalesInventoryDB_V1_Backup.bak'
WITH FORMAT, INIT, NAME = 'V1 Backup Before Optimization';
GO

-- Create V2 database as copy of V1
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SalesInventoryDB_V2')
BEGIN
    CREATE DATABASE SalesInventoryDB_V2;
END
GO

-- Restore V1 data into V2 (if you want separate databases)
 RESTORE DATABASE SalesInventoryDB_V2 FROM DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\SalesInventoryDB_V1_Backup.bak'
 WITH 
	MOVE 'SalesInventoryDB_V1' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SalesInventoryDB_V2.mdf',
	 MOVE 'SalesInventoryDB_V1_log' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SalesInventoryDB_V2_log.ldf';

-- OR OPTION 2: Simply use existing V1 database and rename
-- EXEC sp_renamedb 'SalesInventoryDB_V1', 'SalesInventoryDB_V2';

USE SalesInventoryDB_V2;
GO

-- =====================================================
-- PART 1: ADD INDEXES (Critical for Performance!)
-- =====================================================

PRINT '========================================';
PRINT 'Adding Database Indexes...';
PRINT '========================================';

-- 1. Products Table Indexes

-- Foreign key index (speeds up joins with Categories)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_CategoryId' AND object_id = OBJECT_ID('Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_CategoryId 
    ON Products(CategoryId)
    INCLUDE (ProductName, UnitPrice, StockQuantity, IsActive);
END

-- Search index (speeds up product name searches)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_ProductName' AND object_id = OBJECT_ID('Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_ProductName 
    ON Products(ProductName)
    INCLUDE (CategoryId, UnitPrice, StockQuantity);
    PRINT '✓ IX_Products_ProductName created';
END

-- Active products filter index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_IsActive' AND object_id = OBJECT_ID('Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_IsActive 
    ON Products(IsActive)
    WHERE IsActive = 1;
END

-- Low stock report index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_StockQuantity' AND object_id = OBJECT_ID('Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_StockQuantity 
    ON Products(StockQuantity, ReorderLevel)
    INCLUDE (ProductName, CategoryId);
END

-- 2. SalesOrders Table Indexes (MOST CRITICAL!)
PRINT 'Creating indexes on SalesOrders table...';

-- Foreign key index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrders_CustomerId' AND object_id = OBJECT_ID('SalesOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrders_CustomerId 
    ON SalesOrders(CustomerId)
    INCLUDE (OrderNumber, OrderDate, TotalAmount, Status);
END

-- CRITICAL: Date range query index (this is THE most important!)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrders_OrderDate' AND object_id = OBJECT_ID('SalesOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrders_OrderDate 
    ON SalesOrders(OrderDate DESC)
    INCLUDE (CustomerId, OrderNumber, TotalAmount, Status);
END

-- Status filter index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrders_Status' AND object_id = OBJECT_ID('SalesOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrders_Status 
    ON SalesOrders(Status)
    INCLUDE (OrderDate, CustomerId, TotalAmount);
    PRINT '✓ IX_SalesOrders_Status created';
END

-- Composite index for common report filters
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrders_OrderDate_Status' AND object_id = OBJECT_ID('SalesOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrders_OrderDate_Status 
    ON SalesOrders(OrderDate DESC, Status)
    INCLUDE (CustomerId, TotalAmount);
END

-- 3. SalesOrderDetails Table Indexes

-- Foreign key indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrderDetails_OrderId' AND object_id = OBJECT_ID('SalesOrderDetails'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrderDetails_OrderId 
    ON SalesOrderDetails(OrderId)
    INCLUDE (ProductId, Quantity, UnitPrice, SubTotal);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesOrderDetails_ProductId' AND object_id = OBJECT_ID('SalesOrderDetails'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesOrderDetails_ProductId 
    ON SalesOrderDetails(ProductId)
    INCLUDE (OrderId, Quantity, SubTotal);
END

-- 4. Customers Table Indexes

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Customers_CustomerName' AND object_id = OBJECT_ID('Customers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Customers_CustomerName 
    ON Customers(CustomerName)
    INCLUDE (Email, Phone, City);
END


-- =====================================================
-- VERIFY INDEXES
-- =====================================================


SELECT 
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    STUFF((
        SELECT ', ' + COL_NAME(ic.object_id, ic.column_id)
        FROM sys.index_columns ic
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS KeyColumns,
    STUFF((
        SELECT ', ' + COL_NAME(ic.object_id, ic.column_id)
        FROM sys.index_columns ic
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
        FOR XML PATH('')
    ), 1, 2, '') AS IncludedColumns
FROM sys.indexes i
INNER JOIN sys.objects o ON i.object_id = o.object_id
WHERE o.type = 'U' 
  AND i.type_desc != 'HEAP'
  AND o.name IN ('Products', 'SalesOrders', 'SalesOrderDetails', 'Customers', 'Categories')
ORDER BY TableName, IndexName;


GO