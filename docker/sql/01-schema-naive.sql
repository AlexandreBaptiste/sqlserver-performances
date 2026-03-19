-- =============================================================================
-- Script: 01-schema-naive.sql  (run against DbNaive)
-- Purpose: Create the e-commerce schema with ONLY primary key constraints.
--
-- ❌ NAIVE DESIGN CHOICES:
--   - No indexes on foreign key columns  → JOIN / lookup = full table scan
--   - No indexes on filter columns       → WHERE clauses = full table scan
--   - No covering indexes                → every query fetches unneeded pages
--   - No columnstore index               → analytical queries scan all rows
--
-- This intentionally mirrors what a developer produces when they "just get it
-- working" without thinking about query patterns or execution plans.
-- =============================================================================

-- Guard: skip if tables already exist (idempotent re-run)
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL PRINT 'Schema already exists. Skipping.' ;
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL RETURN ;
GO

-- -------------------------
-- Categories (self-referencing hierarchy)
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
    -- ❌ No index on Email  → searching by email = full table scan on 200k rows
    -- ❌ No index on LastName/FirstName → name search = full scan
);
GO

-- -------------------------
-- Addresses  (1-to-1 with Customer)
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
    -- ❌ No index on CustomerId → lookup by customer = full scan on Addresses
);
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
    -- ❌ No composite index on (CategoryId, Price) → catalog filtering = full scan
);
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
    -- ❌ No index on CustomerId → "orders for customer X" = full scan on 1M rows
    -- ❌ No index on OrderDate  → date range queries = full scan
    -- ❌ No index on Status     → status filtering = full scan
);
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
    -- ❌ No index on OrderId   → fetching items for an order = full scan on 3M rows
    -- ❌ No index on ProductId → product-based analytics = full scan
    -- ❌ No columnstore index  → aggregation queries scan every row store-style
);
GO

PRINT 'DbNaive schema created successfully.';
GO
