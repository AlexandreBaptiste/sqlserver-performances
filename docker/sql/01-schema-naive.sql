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

-- =============================================================================
-- Scenario 11: Over-indexed table (added separately so re-runs are idempotent)
-- =============================================================================
IF OBJECT_ID(N'dbo.ProductReviews', N'U') IS NOT NULL
    PRINT 'ProductReviews already exists in DbNaive. Skipping.'
ELSE
BEGIN
    -- ❌ ANTI-PATTERN: 10 indexes on a write-heavy table.
    --    Every INSERT, UPDATE, and DELETE must maintain ALL of these B-trees.
    --    An INSERT into a table with 10 non-clustered indexes requires
    --    ~11 B-tree page splits/updates (1 clustered + 10 non-clustered).

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

    -- #1 — FK lookup by product
    CREATE NONCLUSTERED INDEX IX_Reviews_ProductId
        ON dbo.ProductReviews (ProductId);

    -- #2 — FK lookup by customer
    CREATE NONCLUSTERED INDEX IX_Reviews_CustomerId
        ON dbo.ProductReviews (CustomerId);

    -- #3 — Filter by rating (only 5 distinct values — very low selectivity)
    CREATE NONCLUSTERED INDEX IX_Reviews_Rating
        ON dbo.ProductReviews (Rating);

    -- #4 — Date sorting
    CREATE NONCLUSTERED INDEX IX_Reviews_CreatedAt
        ON dbo.ProductReviews (CreatedAt DESC);

    -- #5 — ❌ IsVerifiedPurchase is a bit flag (2 distinct values) — near-useless index
    CREATE NONCLUSTERED INDEX IX_Reviews_IsVerifiedPurchase
        ON dbo.ProductReviews (IsVerifiedPurchase);

    -- #6 — HelpfulVotes sort — rarely used alone without other predicates
    CREATE NONCLUSTERED INDEX IX_Reviews_HelpfulVotes
        ON dbo.ProductReviews (HelpfulVotes DESC);

    -- #7 — ❌ Redundant: (ProductId, Rating) is a superset of #1 (ProductId alone)
    --       SQL Server will rarely use #1 when #7 or #8 exist
    CREATE NONCLUSTERED INDEX IX_Reviews_ProductId_Rating
        ON dbo.ProductReviews (ProductId, Rating DESC);

    -- #8 — ❌ Redundant: overlaps with #1 and #4
    CREATE NONCLUSTERED INDEX IX_Reviews_ProductId_CreatedAt
        ON dbo.ProductReviews (ProductId, CreatedAt DESC);

    -- #9 — ❌ Redundant: overlaps with #3 and #5
    CREATE NONCLUSTERED INDEX IX_Reviews_Rating_Verified
        ON dbo.ProductReviews (Rating, IsVerifiedPurchase);

    -- #10 — Title has high cardinality but is never searched alone; wastes index space
    CREATE NONCLUSTERED INDEX IX_Reviews_Title
        ON dbo.ProductReviews (Title);

    PRINT 'ProductReviews (over-indexed, 10 indexes) table created in DbNaive.';
END
GO

-- =============================================================================
-- Scenario 12 — Random GUID PK (anti-pattern)
-- AuditLogs with UNIQUEIDENTIFIER PK generated by NEWID() (random GUIDs).
-- Application code uses Guid.NewGuid() which produces the same random distribution.
-- Random GUIDs cause ~50% page-fill fragmentation and page splits on every row
-- because each new GUID must be inserted in the middle of the clustered B-tree.
-- =============================================================================
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NOT NULL
    PRINT 'AuditLogs already exists in DbNaive. Skipping.'
ELSE
BEGIN
    -- ❌ UNIQUEIDENTIFIER clustered PK — identical schema to DbOptimized.
    --    The performance anti-pattern is in the application code:
    --    Guid.NewGuid() generates random values → INSERT always finds a random
    --    position in the B-tree → ~50% page splits → high I/O overhead.
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

    PRINT 'AuditLogs (random GUID PK anti-pattern) table created in DbNaive.';
END
GO
