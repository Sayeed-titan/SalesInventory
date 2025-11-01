
USE SalesInventoryDB_V2;
GO

IF OBJECT_ID('usp_GetSalesReport', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetSalesReport;
GO

CREATE PROCEDURE usp_GetSalesReport
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Summary Statistics
    SELECT 
        COUNT(DISTINCT so.OrderId) AS TotalOrders,
        ISNULL(SUM(so.TotalAmount), 0) AS TotalRevenue,
        COUNT(DISTINCT CASE WHEN so.Status = 'Completed' THEN so.OrderId END) AS CompletedOrders,
        COUNT(DISTINCT CASE WHEN so.Status = 'Pending' THEN so.OrderId END) AS PendingOrders,
        COUNT(DISTINCT CASE WHEN so.Status = 'Cancelled' THEN so.OrderId END) AS CancelledOrders,
        AVG(so.TotalAmount) AS AverageOrderValue
    FROM SalesOrders so
    WHERE so.OrderDate >= @StartDate 
      AND so.OrderDate <= @EndDate;
    
    -- Top 10 Products by Revenue
    SELECT TOP 10
        p.ProductId,
        p.ProductName,
        SUM(sod.Quantity) AS TotalQuantitySold,
        SUM(sod.SubTotal) AS TotalRevenue,
        COUNT(DISTINCT sod.OrderId) AS OrderCount
    FROM SalesOrderDetails sod
    INNER JOIN Products p ON sod.ProductId = p.ProductId
    INNER JOIN SalesOrders so ON sod.OrderId = so.OrderId
    WHERE so.OrderDate >= @StartDate 
      AND so.OrderDate <= @EndDate
    GROUP BY p.ProductId, p.ProductName
    ORDER BY TotalRevenue DESC;
    
    -- Top 10 Customers by Total Spent
    SELECT TOP 10
        c.CustomerId,
        c.CustomerName,
        c.Email,
        c.City,
        COUNT(DISTINCT so.OrderId) AS TotalOrders,
        SUM(so.TotalAmount) AS TotalSpent,
        AVG(so.TotalAmount) AS AverageOrderValue
    FROM SalesOrders so
    INNER JOIN Customers c ON so.CustomerId = c.CustomerId
    WHERE so.OrderDate >= @StartDate 
      AND so.OrderDate <= @EndDate
    GROUP BY c.CustomerId, c.CustomerName, c.Email, c.City
    ORDER BY TotalSpent DESC;
    
    -- Revenue by Category
    SELECT 
        cat.CategoryId,
        cat.CategoryName,
        SUM(sod.SubTotal) AS TotalRevenue,
        SUM(sod.Quantity) AS TotalQuantity,
        COUNT(DISTINCT sod.OrderId) AS OrderCount
    FROM SalesOrderDetails sod
    INNER JOIN Products p ON sod.ProductId = p.ProductId
    INNER JOIN Categories cat ON p.CategoryId = cat.CategoryId
    INNER JOIN SalesOrders so ON sod.OrderId = so.OrderId
    WHERE so.OrderDate >= @StartDate 
      AND so.OrderDate <= @EndDate
    GROUP BY cat.CategoryId, cat.CategoryName
    ORDER BY TotalRevenue DESC;
    
    -- Daily Revenue Trend (for charts)
    SELECT 
        CAST(so.OrderDate AS DATE) AS OrderDate,
        COUNT(DISTINCT so.OrderId) AS OrderCount,
        SUM(so.TotalAmount) AS DailyRevenue
    FROM SalesOrders so
    WHERE so.OrderDate >= @StartDate 
      AND so.OrderDate <= @EndDate
    GROUP BY CAST(so.OrderDate AS DATE)
    ORDER BY OrderDate;
END
GO

-- =====================================================
-- SP 2: Get Low Stock Products
-- =====================================================
IF OBJECT_ID('usp_GetLowStockProducts', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetLowStockProducts;
GO

CREATE PROCEDURE usp_GetLowStockProducts
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.ProductId,
        p.ProductName,
        c.CategoryName,
        p.UnitPrice,
        p.StockQuantity,
        p.ReorderLevel,
        (p.ReorderLevel - p.StockQuantity) AS QuantityToOrder
    FROM Products p
    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
    WHERE p.IsActive = 1 
      AND p.StockQuantity <= p.ReorderLevel
    ORDER BY p.StockQuantity ASC, p.ProductName;
END
GO

-- =====================================================
-- SP 3: Get Products with Filters (Server-side filtering!)
-- =====================================================
IF OBJECT_ID('usp_GetProducts', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetProducts;
GO

CREATE PROCEDURE usp_GetProducts
    @SearchTerm NVARCHAR(200) = NULL,
    @CategoryId INT = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Server-side filtering and pagination
    SELECT 
        p.ProductId,
        p.ProductName,
        p.CategoryId,
        c.CategoryName,
        p.UnitPrice,
        p.StockQuantity,
        p.ReorderLevel,
        p.IsActive,
        p.CreatedDate
    FROM Products p
    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
    WHERE 
        (@SearchTerm IS NULL OR p.ProductName LIKE '%' + @SearchTerm + '%')
        AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
    ORDER BY p.ProductName
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
    
    -- Return total count for pagination
    SELECT COUNT(*) AS TotalCount
    FROM Products p
    WHERE 
        (@SearchTerm IS NULL OR p.ProductName LIKE '%' + @SearchTerm + '%')
        AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId);
END
GO


-- =====================================================
-- SP 4: Get Sales Orders with Filters
-- =====================================================
IF OBJECT_ID('usp_GetSalesOrders', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetSalesOrders;
GO

CREATE PROCEDURE usp_GetSalesOrders
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @Status NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        so.OrderId,
        so.OrderNumber,
        so.CustomerId,
        c.CustomerName,
        c.Email,
        so.OrderDate,
        so.TotalAmount,
        so.Status,
        (SELECT COUNT(*) FROM SalesOrderDetails WHERE OrderId = so.OrderId) AS ItemCount
    FROM SalesOrders so
    INNER JOIN Customers c ON so.CustomerId = c.CustomerId
    WHERE 
        (@StartDate IS NULL OR so.OrderDate >= @StartDate)
        AND (@EndDate IS NULL OR so.OrderDate <= @EndDate)
        AND (@Status IS NULL OR so.Status = @Status)
    ORDER BY so.OrderDate DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
    
    -- Return total count
    SELECT COUNT(*) AS TotalCount
    FROM SalesOrders so
    WHERE 
        (@StartDate IS NULL OR so.OrderDate >= @StartDate)
        AND (@EndDate IS NULL OR so.OrderDate <= @EndDate)
        AND (@Status IS NULL OR so.Status = @Status);
END
GO


-- =====================================================
-- SP 5: Get Order Details (for details page)
-- =====================================================
IF OBJECT_ID('usp_GetOrderDetails', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetOrderDetails;
GO

CREATE PROCEDURE usp_GetOrderDetails
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Order header
    SELECT 
        so.OrderId,
        so.OrderNumber,
        so.OrderDate,
        so.Status,
        so.TotalAmount,
        so.CustomerId,
        c.CustomerName,
        c.Email,
        c.Phone,
        c.Address,
        c.City,
        c.Country
    FROM SalesOrders so
    INNER JOIN Customers c ON so.CustomerId = c.CustomerId
    WHERE so.OrderId = @OrderId;
    
    -- Order line items
    SELECT 
        sod.OrderDetailId,
        sod.ProductId,
        p.ProductName,
        c.CategoryName,
        sod.Quantity,
        sod.UnitPrice,
        sod.SubTotal
    FROM SalesOrderDetails sod
    INNER JOIN Products p ON sod.ProductId = p.ProductId
    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
    WHERE sod.OrderId = @OrderId
    ORDER BY sod.OrderDetailId;
END
GO


-- =====================================================
-- SP 6: Create Sales Order (with transaction)
-- =====================================================
IF OBJECT_ID('usp_CreateSalesOrder', 'P') IS NOT NULL
    DROP PROCEDURE usp_CreateSalesOrder;
GO

CREATE PROCEDURE usp_CreateSalesOrder
    @CustomerId INT,
    @OrderItems NVARCHAR(MAX) -- JSON format: [{"ProductId":1,"Quantity":5},...]
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    
    BEGIN TRY
        DECLARE @OrderId INT;
        DECLARE @OrderNumber NVARCHAR(50);
        DECLARE @TotalAmount DECIMAL(18,2) = 0;
        
        -- Generate order number
        SET @OrderNumber = 'ORD-' + FORMAT(GETDATE(), 'yyyyMMddHHmmss');
        
        -- Insert order header
        INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, TotalAmount, Status, CreatedDate)
        VALUES (@OrderNumber, @CustomerId, GETDATE(), 0, 'Pending', GETDATE());
        
        SET @OrderId = SCOPE_IDENTITY();
        
        -- Parse JSON and insert order details
        INSERT INTO SalesOrderDetails (OrderId, ProductId, Quantity, UnitPrice, SubTotal)
        SELECT 
            @OrderId,
            ProductId,
            Quantity,
            p.UnitPrice,
            (Quantity * p.UnitPrice) AS SubTotal
        FROM OPENJSON(@OrderItems)
        WITH (
            ProductId INT '$.ProductId',
            Quantity INT '$.Quantity'
        ) AS OrderData
        INNER JOIN Products p ON OrderData.ProductId = p.ProductId;
        
        -- Calculate total
        SELECT @TotalAmount = SUM(SubTotal)
        FROM SalesOrderDetails
        WHERE OrderId = @OrderId;
        
        -- Update order total
        UPDATE SalesOrders
        SET TotalAmount = @TotalAmount
        WHERE OrderId = @OrderId;
        
        COMMIT TRANSACTION;
        
        -- Return created order ID
        SELECT @OrderId AS OrderId, @OrderNumber AS OrderNumber;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO


-- =====================================================
-- TEST STORED PROCEDURES
-- =====================================================

-- Test 1: Sales Report
EXEC usp_GetSalesReport 
    @StartDate = '2024-01-01', 
    @EndDate = '2024-12-31';


-- Test 2: Low Stock
EXEC usp_GetLowStockProducts;


-- Test 3: Get Products
EXEC usp_GetProducts 
    @SearchTerm = 'Product', 
    @CategoryId = NULL, 
    @PageNumber = 1, 
    @PageSize = 10;

GO