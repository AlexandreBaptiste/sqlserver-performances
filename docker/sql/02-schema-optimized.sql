-- =============================================================================
-- Script: 02-schema-optimized.sql  (run against DbOptimized)
-- Purpose: Same e-commerce schema as DbNaive PLUS a full set of carefully
--          designed indexes that address every real-world query pattern.
--
-- ✅ OPTIMIZED DESIGN CHOICES:
--   - Non-clustered indexes on ALL foreign key columns
--   - Composite indexes matching the most frequent WHERE + ORDER BY clauses
--   - Covering indexes (INCLUDE columns) to eliminate key lookups
--   - A non-clustered COLUMNSTORE index on OrderItems for analytical queries
--   - Filtered index on recently-active orders
--
-- Each index is annotated with WHY it exists and WHICH scenario benefits.
-- =============================================================================

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL PRINT 'Schema already exists. Skipping.' ;
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL RETURN ;
GO

-- -------------------------
-- Categories
-- -------------------------
CREATE TABLE dbo.Categories
(
    Id               INT           IDENTITY(1,1) NOT NULL,
    Name             NVARCHAR(100) NOT NULL,
    ParentCategoryId INT           NULL,
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentCategoryId)
        REFERENCES dbo.Categories (Id)
);
GO

-- ✅ Index: look up subcategories by parent
CREATE NONCLUSTERED INDEX IX_Categories_ParentCategoryId
    ON dbo.Categories (ParentCategoryId);
GO

-- -------------------------
-- Customers
-- -------------------------
CREATE TABLE dbo.Customers
(
    Id        INT           IDENTITY(1,1) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName  NVARCHAR(100) NOT NULL,
    Email     NVARCHAR(255) NOT NULL,
    Phone     NVARCHAR(20)  NULL,
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id)
);
GO

-- ✅ Scenario 1: Email lookup / prefix search  →  index seek instead of scan
CREATE NONCLUSTERED INDEX IX_Customers_Email
    ON dbo.Customers (Email)
    INCLUDE (FirstName, LastName);
GO

-- ✅ Scenario 1: Name search  →  composite allows (LastName, FirstName) queries
CREATE NONCLUSTERED INDEX IX_Customers_LastName_FirstName
    ON dbo.Customers (LastName, FirstName)
    INCLUDE (Email);
GO

-- ✅ General: date-based customer queries ("new signups this month")
CREATE NONCLUSTERED INDEX IX_Customers_CreatedAt
    ON dbo.Customers (CreatedAt);
GO

-- -------------------------
-- Addresses
-- -------------------------
CREATE TABLE dbo.Addresses
(
    Id         INT           IDENTITY(1,1) NOT NULL,
    CustomerId INT           NOT NULL,
    Street     NVARCHAR(255) NOT NULL,
    City       NVARCHAR(100) NOT NULL,
    PostalCode NVARCHAR(20)  NOT NULL,
    Country    NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Addresses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Addresses_Customer FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (Id) ON DELETE CASCADE
);
GO

-- ✅ Scenario 2 / FK: 1-to-1 address lookup by customer  →  no scan on Addresses
CREATE UNIQUE NONCLUSTERED INDEX IX_Addresses_CustomerId
    ON dbo.Addresses (CustomerId);
GO

-- -------------------------
-- Products
-- -------------------------
CREATE TABLE dbo.Products
(
    Id          INT             IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(255)   NOT NULL,
    Description NVARCHAR(MAX)   NULL,
    Price       DECIMAL(18, 2)  NOT NULL,
    Stock       INT             NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0,
    CategoryId  INT             NOT NULL,
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId)
        REFERENCES dbo.Categories (Id)
);
GO

-- ✅ Scenario 3: Product catalog filtered by category AND price range.
--    Composite (CategoryId, Price) + INCLUDE avoids a key lookup entirely.
CREATE NONCLUSTERED INDEX IX_Products_CategoryId_Price
    ON dbo.Products (CategoryId, Price)
    INCLUDE (Name, Stock);
GO

-- -------------------------
-- Orders
-- -------------------------
CREATE TABLE dbo.Orders
(
    Id          INT            IDENTITY(1,1) NOT NULL,
    CustomerId  INT            NOT NULL,
    OrderDate   DATETIME2      NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT GETUTCDATE(),
    Status      TINYINT        NOT NULL CONSTRAINT DF_Orders_Status DEFAULT 0,
    TotalAmount DECIMAL(18, 2) NOT NULL,
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Orders_Customer FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (Id)
);
GO

-- ✅ Scenario 2: "All orders for customer X"  →  index seek on 1M-row table
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId
    ON dbo.Orders (CustomerId)
    INCLUDE (OrderDate, Status, TotalAmount);
GO

-- ✅ Scenario 4 / 7: Date-range queries and monthly report GROUP BY
CREATE NONCLUSTERED INDEX IX_Orders_OrderDate
    ON dbo.Orders (OrderDate)
    INCLUDE (CustomerId, TotalAmount, Status);
GO

-- ✅ Scenario 6: Pagination by Id (keyset) & status filtering
CREATE NONCLUSTERED INDEX IX_Orders_Status_Id
    ON dbo.Orders (Status, Id)
    INCLUDE (CustomerId, OrderDate, TotalAmount);
GO

-- ✅ Filtered index: only keep non-cancelled orders that are still active.
--    Dramatically reduces index size; ideal for operational dashboards.
CREATE NONCLUSTERED INDEX IX_Orders_Active
    ON dbo.Orders (OrderDate, CustomerId)
    INCLUDE (TotalAmount, Status)
    WHERE Status <> 4; -- 4 = Cancelled
GO

-- -------------------------
-- OrderItems
-- -------------------------
CREATE TABLE dbo.OrderItems
(
    Id        INT            IDENTITY(1,1) NOT NULL,
    OrderId   INT            NOT NULL,
    ProductId INT            NOT NULL,
    Quantity  INT            NOT NULL,
    UnitPrice DECIMAL(18, 2) NOT NULL,
    CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Product FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (Id)
);
GO

-- ✅ Scenario 5 (N+1): Fetching all items for a given order  →  index seek
CREATE NONCLUSTERED INDEX IX_OrderItems_OrderId
    ON dbo.OrderItems (OrderId)
    INCLUDE (ProductId, Quantity, UnitPrice);
GO

-- ✅ Scenario 4: Product-level revenue analytics
CREATE NONCLUSTERED INDEX IX_OrderItems_ProductId
    ON dbo.OrderItems (ProductId)
    INCLUDE (Quantity, UnitPrice, OrderId);
GO

-- ✅ Scenario 4 / 7: Non-clustered COLUMNSTORE index for analytical aggregations.
--    SQL Server can batch-process millions of rows using SIMD via this index.
--    Provides 10-100x speedup for GROUP BY / SUM queries over OrderItems.
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OrderItems_Columnstore
    ON dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice);
GO

PRINT 'DbOptimized schema created successfully.';
GO

-- =============================================================================
-- Scenario 11: Properly-indexed table (added separately so re-runs are idempotent)
-- =============================================================================
IF OBJECT_ID(N'dbo.ProductReviews', N'U') IS NOT NULL
    PRINT 'ProductReviews already exists in DbOptimized. Skipping.'
ELSE
BEGIN
    -- ✅ BEST PRACTICE: 2 targeted covering indexes matching actual query patterns.
    --    Every INSERT, UPDATE, and DELETE maintains only 3 B-trees
    --    (1 clustered PK + 2 non-clustered), minimising write overhead.

    CREATE TABLE dbo.ProductReviews
    (
        Id                 INT            IDENTITY(1,1) NOT NULL,
        ProductId          INT            NOT NULL,
        CustomerId         INT            NOT NULL,
        Rating             TINYINT        NOT NULL,
        Title              NVARCHAR(200)  NOT NULL,
        Body               NVARCHAR(2000) NOT NULL,
        CreatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT GETUTCDATE(),
        HelpfulVotes       INT            NOT NULL CONSTRAINT DF_Reviews_HelpfulVotes DEFAULT 0,
        IsVerifiedPurchase BIT            NOT NULL CONSTRAINT DF_Reviews_IsVerifiedPurchase DEFAULT 0,
        CONSTRAINT PK_ProductReviews PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Reviews_Product  FOREIGN KEY (ProductId)  REFERENCES dbo.Products(Id),
        CONSTRAINT FK_Reviews_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
        CONSTRAINT CK_Reviews_Rating   CHECK (Rating BETWEEN 1 AND 5)
    );

    -- ✅ Covers: WHERE ProductId = ? AND Rating >= ? ORDER BY Rating DESC
    --    INCLUDE provides all projected columns → zero key-lookups
    CREATE NONCLUSTERED INDEX IX_Reviews_ProductId_Rating
        ON dbo.ProductReviews (ProductId, Rating DESC)
        INCLUDE (CustomerId, Title, CreatedAt, IsVerifiedPurchase, HelpfulVotes);

    -- ✅ Covers: WHERE CustomerId = ? ORDER BY CreatedAt DESC
    CREATE NONCLUSTERED INDEX IX_Reviews_CustomerId_CreatedAt
        ON dbo.ProductReviews (CustomerId, CreatedAt DESC)
        INCLUDE (ProductId, Rating, Title);

    PRINT 'ProductReviews (properly indexed, 2 indexes) table created in DbOptimized.';
END
GO

-- =============================================================================
-- Scenario 12 — Sequential GUID PK (best practice)
-- AuditLogs with UNIQUEIDENTIFIER PK generated by Guid.CreateVersion7() (.NET 9+).
-- Version 7 UUIDs embed a millisecond timestamp in the most-significant bits,
-- which ensures monotonically increasing inserts → no page splits → sequential I/O.
-- =============================================================================
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NOT NULL
    PRINT 'AuditLogs already exists in DbOptimized. Skipping.'
ELSE
BEGIN
    -- ✅ Identical schema to DbNaive — the performance gain comes purely from
    --    the application code using Guid.CreateVersion7() instead of Guid.NewGuid().
    --    Version 7 GUIDs are time-ordered: each new GUID is always greater than the
    --    previous one → INSERT always appends to the end of the B-tree → no page splits.
    CREATE TABLE dbo.AuditLogs
    (
        Id                    UNIQUEIDENTIFIER NOT NULL,
        EntityName            NVARCHAR(100)    NOT NULL,
        EntityId              INT              NOT NULL,
        Action                NVARCHAR(50)     NOT NULL,
        OldValues             NVARCHAR(4000)   NULL,
        NewValues             NVARCHAR(4000)   NULL,
        ChangedByCustomerId   INT              NOT NULL,
        Timestamp             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AuditLogs_Customers
            FOREIGN KEY (ChangedByCustomerId) REFERENCES dbo.Customers(Id)
    );

    -- Index on Timestamp for chronological queries
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp
        ON dbo.AuditLogs (Timestamp DESC)
        INCLUDE (EntityName, EntityId, Action);

    PRINT 'AuditLogs (sequential GUID PK best practice) table created in DbOptimized.';
END
GO
