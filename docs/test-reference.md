# Test & Optimization Reference Guide

> Complete reference of every scenario in the project — what each test validates,
> how the naive approach differs from the optimized one, why the optimization
> matters, and the expected performance gains.
>
> **Dataset:** ~200 000 customers · ~1 000 000 orders · ~3 000 000 order items · SQL Server 2022

---

## Table of Contents

| # | Scenario | Section |
|---|----------|---------|
| 1 | [Customer Search](#scenario-1--customer-search) | `LIKE '%x%'` vs `LIKE 'x%'` + index seek |
| 2 | [Order History](#scenario-2--order-history) | Unindexed FK vs covering index |
| 3 | [Product Catalog](#scenario-3--product-catalog) | Full scan + in-memory filter vs composite covering index |
| 4 | [Sales Report](#scenario-4--sales-report) | Row-store aggregation vs columnstore + Dapper |
| 5 | [N+1 Problem](#scenario-5--n1-problem) | N+1 round-trips vs `AsSplitQuery` |
| 6 | [Deep Pagination](#scenario-6--deep-pagination) | OFFSET/FETCH vs keyset pagination |
| 7 | [Complex Join](#scenario-7--complex-join) | Cartesian product vs `AsSplitQuery` + FK indexes |
| 8 | [Bulk Insert](#scenario-8--bulk-insert) | SaveChanges per entity vs AddRange / SqlBulkCopy |
| 9 | [Count() vs Any()](#scenario-9--existence-check-count-vs-any) | `Count() > 0` vs `Any()` (EXISTS) |
| 10 | [Tracking vs NoTracking](#scenario-10--tracking-vs-notracking) | Change-tracking overhead vs `AsNoTracking` + DTO |
| 11 | [Over-Indexed Table](#scenario-11--over-indexed-table) | 10 redundant indexes vs 2 targeted covering indexes |
| 12 | [Random vs Sequential GUID PK](#scenario-12--random-vs-sequential-guid-pk) | `Guid.NewGuid()` page splits vs `Guid.CreateVersion7()` sequential inserts |

---

## Scenario 1 — Customer Search

### The Problem

Searching customers by email or last name on a table with 200 000 rows.

### Naive Approach — `NaiveCustomerQueries`

```csharp
// ❌ Contains() → LIKE '%term%' → full table scan (200k rows)
// ❌ No AsNoTracking() → every entity tracked
// ❌ Returns full Customer entity (SELECT *)
context.Customers.Where(c => c.Email.Contains(searchTerm)).ToListAsync();
```

**What goes wrong:**

- `Contains("john")` translates to `WHERE Email LIKE '%john%'`. The **leading wildcard** (`%`) prevents SQL Server from using *any* index — even if one exists on `Email`. It must scan every row.
- Without `AsNoTracking()`, EF Core allocates a snapshot copy, identity-map entry, and state object for each of the 200 000 rows — wasted CPU and memory for a read-only query.
- `SELECT *` fetches every column, including ones the caller never uses.

### Optimized Approach — `OptimizedCustomerQueries`

```csharp
// ✅ StartsWith() → LIKE 'term%' → index seek on IX_Customers_Email
// ✅ Compiled query → LINQ tree parsed once, reused on every call
// ✅ AsNoTracking() + Select projection → minimal memory
context.Customers
    .AsNoTracking()
    .Where(c => c.Email.StartsWith(prefix))
    .Select(c => new CustomerSearchResult(c.Id, c.FirstName, c.LastName, c.Email));
```

**Why it's better:**

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| SQL pattern | `LIKE '%x%'` (full scan) | `LIKE 'x%'` (index seek) |
| Index used | None | `IX_Customers_Email` |
| Tracking | Full snapshot per entity | None (`AsNoTracking`) |
| Columns returned | All (`SELECT *`) | 4 columns (projection) |
| LINQ compilation | Every call | Once (compiled query) |

**Expected gain: 10×–50× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Email search: both return at least one result | Correctness | `CustomerSearchTests.cs` | Both naive and optimized return non-empty results for "john" |
| Email search: optimized result is subset of naive | Correctness | `CustomerSearchTests.cs` | Every prefix match (StartsWith) appears in the Contains result set |
| LastName search: both return at least one result | Correctness | `CustomerSearchTests.cs` | Both implementations work for "Sm" |
| Optimized must complete in < 500 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold guard |
| Optimized must be at least 2× faster than naive | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Naive vs Optimized | Benchmark | `CustomerSearchBenchmark.cs` | Statistical comparison with memory diagnostics |

---

## Scenario 2 — Order History

### The Problem

Loading all orders for a given customer from a 1 000 000-row `Orders` table.

### Naive Approach — `NaiveOrderQueries.GetOrdersByCustomerAsync`

```csharp
// ❌ No index on CustomerId → full table scan on 1M rows
// ❌ No AsNoTracking() → tracks every entity
// ❌ Returns full Order entity
context.Orders.Where(o => o.CustomerId == customerId).ToListAsync();
```

**What goes wrong:**

Without an index on `Orders.CustomerId`, SQL Server must read every single row in the 1M-row table to find the ~5 rows belonging to one customer. This is like searching through an entire phone book instead of jumping to the right letter.

### Optimized Approach — `OptimizedOrderQueries.GetOrdersByCustomerAsync`

```csharp
// ✅ IX_Orders_CustomerId INCLUDE (OrderDate, Status, TotalAmount) → index seek
// ✅ AsNoTracking + projection → DTO with only relevant columns
context.Orders
    .AsNoTracking()
    .Where(o => o.CustomerId == customerId)
    .Select(o => new OrderSummary(o.Id, o.OrderDate, o.Status.ToString(), o.TotalAmount, o.Items.Count));
```

**Why it's better:**

The **covering index** `IX_Orders_CustomerId INCLUDE (OrderDate, Status, TotalAmount)` lets SQL Server:
1. **Seek** directly to the customer's rows (O(log n) instead of O(n))
2. Read all needed columns from the index itself — **zero key-lookups** back to the data pages

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| Access pattern | Full table scan (1M rows) | Index seek (~5 rows) |
| Key-lookups | N/A (scanning data pages) | 0 (covering index) |
| Tracking | Full | None |
| Columns | All | 5 (projection) |

**Expected gain: 20×–100× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Both return same order IDs | Correctness | `OrderHistoryTests.cs` | Same data regardless of approach |
| Optimized must complete in < 200 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| Optimized must be at least 2× faster | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Naive vs Optimized | Benchmark | `OrderHistoryBenchmark.cs` | Statistical comparison |

---

## Scenario 3 — Product Catalog

### The Problem

Filtering products by category *and* price range, then counting products per category.

### Naive Approach — `NaiveProductQueries`

```csharp
// ❌ No composite index → two separate filter scans
// ❌ Loads Description (NVARCHAR(MAX)) — large column never needed
// ❌ CountPerCategory: loads ALL products into memory THEN groups in C#
var all = await context.Products.ToListAsync();
return all.GroupBy(p => p.CategoryId).ToDictionary(g => g.Key, g => g.Count());
```

**What goes wrong:**

- Without a composite index on `(CategoryId, Price)`, SQL Server cannot efficiently satisfy both filter conditions at once.
- `ToList()` before `GroupBy` fetches every product row (including `Description` — potentially KB of text per row) into application memory, then groups in C#. The database engine is far more efficient at this.

### Optimized Approach — `OptimizedProductQueries`

```csharp
// ✅ IX_Products_CategoryId_Price INCLUDE (Name, Stock) → covering index seek
// ✅ GroupBy pushed to SQL, not C#
// ✅ AsNoTracking + projection
context.Products.AsNoTracking()
    .Where(p => p.CategoryId == categoryId && p.Price >= minPrice && p.Price <= maxPrice)
    .Select(p => new ProductCatalogItem(p.Id, p.Name, p.Category.Name, p.Price, p.Stock));
```

**Why it's better:**

The composite index `(CategoryId, Price) INCLUDE (Name, Stock)` is a **covering index**: SQL Server can satisfy the entire WHERE clause *and* the SELECT projection from the index pages alone — it never touches the data pages (no key-lookups).

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| Filter | Scan entire table | Index seek on composite key |
| Key-lookups | N/A (full scan) | 0 (covering index) |
| Columns loaded | All (including NVARCHAR(MAX)) | 5 lightweight columns |
| GroupBy location | C# (after full load) | SQL Server (server-side) |

**Expected gain: 3×–10× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Same product IDs returned | Correctness | `ProductCatalogTests.cs` | Identical results from both approaches |
| All results satisfy price range constraint | Correctness | `ProductCatalogTests.cs` | No incorrect rows returned |
| Both return same category totals | Correctness | `ProductCatalogTests.cs` | GroupBy correctness |
| Optimized covering-index query < 300 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| CountPerCategory: optimized ≥ naive | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Naive vs Optimized | Benchmark | `ProductCatalogBenchmark.cs` | Statistical comparison |

---

## Scenario 4 — Sales Report

### The Problem

Aggregating revenue across 3 000 000 order items joined to orders, products, and categories.

### Naive Approach — `NaiveSalesReportQueries`

```csharp
// ❌ No columnstore index → row-store aggregation one row at a time
// ❌ Full scan on OrderDate (non-sargable Year extraction)
// ❌ LINQ joins translated to inefficient nested-loop joins
context.OrderItems
    .Where(i => i.Order.OrderDate.Year == year)
    .GroupBy(...)
    .Select(g => new MonthlySalesItem(...));
```

**What goes wrong:**

- Without a **columnstore index**, SQL Server processes rows one at a time in *row-store mode*. For 3M rows, this is dramatically slow.
- `i.Order.OrderDate.Year == year` wraps the column in a function (`YEAR()`), making it **non-sargable** — the database cannot use an index on `OrderDate`.

### Optimized Approach — `OptimizedSalesReportQueries`

```csharp
// ✅ Dapper + raw SQL → bypasses EF Core materialization overhead
// ✅ Columnstore index → batch-mode execution (900+ rows/CPU cycle)
// ✅ Sargable date range: WHERE o.OrderDate >= @YearStart AND o.OrderDate < @YearEnd
const string sql = """
    SELECT YEAR(o.OrderDate) AS Year, MONTH(o.OrderDate) AS Month, ...
    FROM dbo.Orders o JOIN dbo.OrderItems oi ON oi.OrderId = o.Id ...
    WHERE o.OrderDate >= @YearStart AND o.OrderDate < @YearEnd
    GROUP BY ...
""";
await conn.QueryAsync<MonthlySalesItem>(new CommandDefinition(sql, new { YearStart, YearEnd }));
```

**Why it's better:**

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| Execution mode | Row-store (1 row/cycle) | Batch-mode via columnstore (900+ rows/cycle) |
| Date filter | `YEAR(col) = @x` (non-sargable) | `col >= @start AND col < @end` (sargable) |
| ORM overhead | EF Core materialization | Dapper (lightweight) |
| Join strategy | LINQ-translated nested loops | Optimized SQL with index hints |

**Expected gain: 5×–50× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Both return same number of results | Correctness | `SalesReportTests.cs` | Result count matches |
| Same product IDs in top 10 | Correctness | `SalesReportTests.cs` | Ranking accuracy |
| Both produce same number of months | Correctness | `SalesReportTests.cs` | Aggregation completeness |
| Optimized Dapper + columnstore < 2000 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| BenchmarkDotNet: Naive vs Optimized | Benchmark | `SalesReportBenchmark.cs` | Statistical comparison |

---

## Scenario 5 — N+1 Problem

### The Problem

Loading 100 orders with their order items. The naive approach fires **101 separate queries**.

### Naive Approach — `NaiveOrderQueries.GetOrdersWithItemsNPlusOneAsync`

```csharp
// ❌ Step 1: load 100 orders (1 query)
var orders = await context.Orders.Take(100).ToListAsync();

foreach (var order in orders)
{
    // ❌ Step 2: for EACH order, fire a separate query (100 queries)
    var items = await context.OrderItems
        .Where(i => i.OrderId == order.Id).ToListAsync();
}
// Total: 101 round-trips to the database
```

**What goes wrong:**

Each database round-trip has a fixed cost (network latency, query parsing, transaction management). With 101 queries, even 5ms of latency per query adds **500ms** of pure waiting time. This is one of the most common performance killers in ORM-based applications.

### Optimized Approach — `OptimizedOrderQueries.GetOrdersWithItemsAsync`

```csharp
// ✅ AsSplitQuery() → exactly 2 SQL queries total
// ✅ IX_OrderItems_OrderId → index seek for the items batch
context.Orders.AsNoTracking().AsSplitQuery()
    .Include(o => o.Items).ThenInclude(i => i.Product)
    .Take(100).ToListAsync();
```

**Why it's better:**

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| SQL queries | 101 (1 + N) | 2 (split query) |
| Network round-trips | 101 | 2 |
| Latency impact | ~500ms at 5ms/query | ~10ms at 5ms/query |
| With 1000 orders | 1001 queries | Still 2 queries |

The **N+1 penalty scales linearly** with the number of parent records. `AsSplitQuery()` always uses a fixed number of queries regardless of N.

**Expected gain: 20×–100× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| N+1 vs AsSplitQuery: same order IDs | Correctness | `OrderHistoryTests.cs` | Same data from both approaches |
| N+1 vs AsSplitQuery: item counts match | Correctness | `OrderHistoryTests.cs` | No lost or duplicated items |
| Optimized at least 5× faster for 100 orders | Performance | `PerformanceAssertionTests.cs` | Ratio guard (conservative) |
| BenchmarkDotNet: N+1 vs AsSplitQuery | Benchmark | `NPlusOneBenchmark.cs` | Statistical comparison |

---

## Scenario 6 — Deep Pagination

### The Problem

Paginating through 1 000 000 orders. At page 50 000, the naive approach must discard 999 980 rows.

### Naive Approach — `NaivePaginationQueries`

```csharp
// ❌ OFFSET forces SQL Server to generate and discard (page-1)*pageSize rows
// ❌ Extra COUNT(*) query doubles the work
context.Orders.OrderBy(o => o.Id)
    .Skip((pageNumber - 1) * pageSize)  // OFFSET 999,980 ROWS
    .Take(pageSize)                      // FETCH NEXT 20 ROWS
    .ToListAsync();
```

**What goes wrong:**

`OFFSET n ROWS` is not free — SQL Server must generate, sort, and then *discard* all n rows before returning the next page. Cost scales **linearly with page depth**: page 1 is fast, page 50 000 scans nearly the entire table.

### Optimized Approach — `OptimizedPaginationQueries`

```csharp
// ✅ Keyset pagination: WHERE Id > @lastSeenId ORDER BY Id
// ✅ Single index seek — O(1) regardless of page depth
var query = context.Orders.AsNoTracking();
if (lastSeenId is not null)
    query = query.Where(o => o.Id > lastSeenId.Value);

query.OrderBy(o => o.Id).Take(pageSize);
```

**Why it's better:**

| Aspect | Naive (page 50k) | Optimized (page 50k) |
|--------|-------------------|----------------------|
| Rows scanned | ~1 000 000 | 20 (the page itself) |
| Time complexity | O(n) per page | O(1) per page |
| Secondary query | COUNT(*) (another full scan) | None |
| Page 1 speed | Fast | Fast |
| Page 50 000 speed | Very slow | Still fast |

**Expected gain: 50×–500× faster at deep pages**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| First page: both return 20 items | Correctness | `PaginationTests.cs` | Page size respected |
| First page: same order IDs | Correctness | `PaginationTests.cs` | Same starting data |
| NextCursorId is set when page is full | Correctness | `PaginationTests.cs` | Cursor mechanics |
| Consecutive pages have no overlap | Correctness | `PaginationTests.cs` | No duplicate rows |
| Keyset page 1 < 100 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| BenchmarkDotNet: OFFSET vs Keyset | Benchmark | `PaginationBenchmark.cs` | Statistical comparison |

---

## Scenario 7 — Complex Join

### The Problem

Loading recent orders with full customer info and all line items (order → customer → items → product → category).

### Naive Approach — `NaiveComplexJoinQueries`

```csharp
// ❌ No AsSplitQuery → Cartesian product (every order column × every item)
// ❌ Loads Product.Description (NVARCHAR(MAX)) — never displayed
// ❌ No FK indexes → nested-loop joins on full table scans
context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
    .OrderByDescending(o => o.OrderDate).Take(50).ToListAsync();
```

**What goes wrong:**

Without `AsSplitQuery()`, EF Core generates a single massive JOIN. If an order has 5 items, **all order and customer columns are duplicated 5 times** in the SQL result set. For 50 orders with 3 items each, that's 150 rows with heavily duplicated data instead of 50 orders + 150 items.

### Optimized Approach — `OptimizedComplexJoinQueries`

```csharp
// ✅ AsSplitQuery → 3 focused queries, no Cartesian product
// ✅ FK indexes → each join is an index seek
// ✅ AsNoTracking — read-only
context.Orders.AsNoTracking().AsSplitQuery()
    .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
    .OrderByDescending(o => o.OrderDate).Take(50).ToListAsync();
```

**Why it's better:**

| Aspect | Naive | Optimized |
|--------|-------|-----------|
| SQL strategy | Single Cartesian JOIN | 3 split queries |
| Result set size | 150 rows (duplicated) | 50 + 150 rows (no duplicates) |
| NVARCHAR(MAX) loaded | Yes | No (projection avoids it) |
| FK index usage | None (scans) | `IX_OrderItems_OrderId` (seeks) |
| Tracking | Full | None |

**Expected gain: 5×–20× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Both return same count | Correctness | `ComplexJoinTests.cs` | Same number of orders |
| Same order IDs in result set | Correctness | `ComplexJoinTests.cs` | Same data |
| Line item counts match per order | Correctness | `ComplexJoinTests.cs` | No lost items |
| Optimized AsSplitQuery < 1000 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| Optimized at least 2× faster | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Cartesian vs Split | Benchmark | `ComplexJoinBenchmark.cs` | Statistical comparison |

---

## Scenario 8 — Bulk Insert

### The Problem

Inserting 10 000 customer rows into the database.

### Naive Approach — `NaiveBulkInsertQueries`

```csharp
foreach (var customer in customers)
{
    // ❌ 1 INSERT + 1 COMMIT per row → 10,000 round-trips
    context.Customers.Add(customer);
    await context.SaveChangesAsync();
}
```

**What goes wrong:**

Each `SaveChangesAsync()` creates a separate SQL transaction, sends one INSERT statement, waits for the server to persist it, and returns the generated ID. For 10 000 rows, that's **10 000 network round-trips** and 10 000 separate transactions.

### Optimized Approaches — `OptimizedBulkInsertQueries`

**Strategy 1: AddRange + single SaveChanges**

```csharp
// ✅ EF Core batches ~42 rows per INSERT statement
context.Customers.AddRange(customers);
await context.SaveChangesAsync(); // ~240 round-trips instead of 10,000
```

**Strategy 2: SqlBulkCopy**

```csharp
// ✅ Single minimally-logged bulk operation
using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "dbo.Customers" };
await bulk.WriteToServerAsync(dataTable); // 1 round-trip for all 10,000 rows
```

**Why it's better:**

| Strategy | Round-trips | Transactions | Speed |
|----------|-------------|-------------|-------|
| Naive (per entity) | 10 000 | 10 000 | Baseline |
| AddRange + SaveChanges | ~240 | 1 | ~40× faster |
| SqlBulkCopy | 1 | 1 (minimally logged) | ~200× faster |

**Expected gain: AddRange ~40×, SqlBulkCopy ~200× faster**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| AddRange inserts all rows correctly | Correctness | `BulkInsertTests.cs` | 50 rows inserted = 50 rows in DB |
| BulkCopy inserts all rows correctly | Correctness | `BulkInsertTests.cs` | 100 rows inserted = 100 rows in DB |
| BenchmarkDotNet: 3-way comparison | Benchmark | `BulkInsertBenchmark.cs` | Naive vs AddRange vs SqlBulkCopy |

---

## Scenario 9 — Existence Check (Count vs Any)

### The Problem

Checking whether a row exists — e.g., "does this email exist?" or "does this customer have orders?"

### Naive Approach — `NaiveExistenceCheckQueries`

```csharp
// ❌ Count() scans ALL matching rows, then compares with 0
return await context.Customers
    .Where(c => c.Email == email)
    .CountAsync() > 0;

// ❌ Count() on FK column without index → full scan + count everything
return await context.Orders
    .Where(o => o.CustomerId == customerId)
    .CountAsync() > 0;
```

**What goes wrong:**

`Count()` forces SQL Server to visit **every matching row** and keep a running total. If a customer has 5 orders among 1 000 000, SQL Server counts all 5. If you only need "is there at least one?", counting is wasted work.

### Optimized Approach — `OptimizedExistenceCheckQueries`

```csharp
// ✅ Any() → SELECT CASE WHEN EXISTS(...) THEN 1 ELSE 0 END
// ✅ SQL Server stops at the FIRST matching row (short-circuit)
return await context.Orders
    .AsNoTracking()
    .AnyAsync(o => o.CustomerId == customerId);
```

**Why it's better:**

| Aspect | `Count() > 0` | `Any()` |
|--------|---------------|---------|
| SQL generated | `SELECT COUNT(*)` | `SELECT CASE WHEN EXISTS(...)` |
| Rows scanned | All matching rows | Stops at first match |
| For 5 matches in 1M rows | Scans to find all 5 | Stops after 1 |
| For 0 matches | Full scan | Full scan (same) |

The difference is most dramatic when the answer is **true** — `Any()` can return immediately, while `Count()` must finish counting everything.

**Expected gain: 2×–10× faster (when matches exist)**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Both agree customer 1 exists | Correctness | `ExistenceCheckTests.cs` | Same boolean result (true) |
| Both agree non-existent email is false | Correctness | `ExistenceCheckTests.cs` | Same boolean result (false) |
| Both agree customer 1 has orders | Correctness | `ExistenceCheckTests.cs` | Same boolean result (true) |
| Both agree non-existent customer has no orders | Correctness | `ExistenceCheckTests.cs` | Same boolean result (false) |
| Any() < 50 ms | Performance | `PerformanceAssertionTests.cs` | Absolute threshold |
| Any() faster than Count() > 0 | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Count vs Any (4 benchmarks) | Benchmark | `ExistenceCheckBenchmark.cs` | Email + orders × naive/optimized |

---

## Scenario 10 — Tracking vs NoTracking

### The Problem

Loading 5 000 customer rows for a read-only list screen. EF Core's change tracker adds significant overhead for data that will never be modified.

### Naive Approach — `NaiveTrackingQueries`

```csharp
// ❌ Default tracking → snapshot + identity map + state entry per entity
// ❌ SELECT * → all columns including unused ones
context.Customers.OrderBy(c => c.Id).Take(5000).ToListAsync();
```

**What goes wrong:**

For every entity loaded with tracking, EF Core:
1. Creates a **snapshot copy** of every property value (for dirty-checking)
2. Adds the entity to an **identity map** (to deduplicate)
3. Creates a **state entry** object (to track Added/Modified/Deleted)

For 5 000 entities, this roughly **doubles** memory allocation and adds measurable CPU time — all wasted if you never call `SaveChanges()`.

### Optimized Approach — `OptimizedTrackingQueries`

```csharp
// ✅ AsNoTracking() → zero tracker overhead
// ✅ Projection → only 4 columns, not SELECT *
context.Customers.AsNoTracking()
    .OrderBy(c => c.Id).Take(5000)
    .Select(c => new CustomerSearchResult(c.Id, c.FirstName, c.LastName, c.Email));
```

**Why it's better:**

| Aspect | Tracked | NoTracking + Projection |
|--------|---------|------------------------|
| Snapshot copies | 5 000 | 0 |
| Identity map entries | 5 000 | 0 |
| State entry objects | 5 000 | 0 |
| Columns fetched | All (including Phone, CreatedAt) | 4 (Id, FirstName, LastName, Email) |
| Memory footprint | ~18 MB | ~4 MB |

**Expected gain: 1.5×–3× faster, ~4× less memory**

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| Same row count | Correctness | `TrackingTests.cs` | Both return 100 rows |
| Same customer IDs | Correctness | `TrackingTests.cs` | Identical data |
| Tracked populates tracker; untracked does not | Correctness | `TrackingTests.cs` | ChangeTracker.Entries() > 0 vs == 0 |
| NoTracking + projection faster than tracked | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: Tracked vs NoTracking | Benchmark | `TrackingBenchmark.cs` | Statistical comparison with MemoryDiagnoser |

---

## Test Summary

### Correctness Tests (27 total)

| File | Scenario | Test Count |
|------|----------|-----------|
| `CustomerSearchTests.cs` | 1 — Customer Search | 3 |
| `OrderHistoryTests.cs` | 2 — Order History + 5 — N+1 | 3 |
| `ProductCatalogTests.cs` | 3 — Product Catalog | 3 |
| `SalesReportTests.cs` | 4 — Sales Report | 3 |
| `PaginationTests.cs` | 6 — Deep Pagination | 4 |
| `ComplexJoinTests.cs` | 7 — Complex Join | 3 |
| `BulkInsertTests.cs` | 8 — Bulk Insert | 2 |
| `ExistenceCheckTests.cs` | 9 — Count vs Any | 4 |
| `TrackingTests.cs` | 10 — Tracking vs NoTracking | 3 |
| `OverIndexedReviewTests.cs` | 11 — Over-Indexed Table | 4 |
| `GuidKeyAuditLogTests.cs` | 12 — Random vs Sequential GUID PK | 4 |

**Purpose:** Verify that naive and optimized approaches produce the **same logical result**. If an optimization changes the data, these tests catch it.

### Performance Tests (14 total)

All in `PerformanceAssertionTests.cs`. Two types:

- **Threshold tests** — "optimized must complete in < X ms" — catches regressions
- **Ratio tests** — "naive must be ≥ N× slower than optimized" — validates the optimization delivers meaningful improvement

### BenchmarkDotNet Scenarios (10 scenarios, 22 benchmarks)

All in `src/DatabasePerformances.Benchmarks/Scenarios/`. Run with:

```bash
dotnet run -c Release --project src/DatabasePerformances.Benchmarks
```

These provide statistically rigorous comparisons with JIT warm-up, iteration statistics, and memory allocation diagnostics via `MemoryDiagnoser`.

---

## Performance Gains at a Glance

| # | Scenario | Key Optimization | Expected Gain |
|---|----------|-----------------|---------------|
| 1 | Customer Search | `StartsWith` + index seek | 10–50× |
| 2 | Order History | Covering FK index | 20–100× |
| 3 | Product Catalog | Composite covering index | 3–10× |
| 4 | Sales Report | Columnstore + Dapper | 5–50× |
| 5 | N+1 Problem | `AsSplitQuery` (2 queries) | 20–100× |
| 6 | Deep Pagination | Keyset `WHERE Id > @cursor` | 50–500× |
| 7 | Complex Join | `AsSplitQuery` + FK indexes | 5–20× |
| 8 | Bulk Insert | `AddRange` / `SqlBulkCopy` | 40–200× |
| 9 | Count vs Any | `Any()` → EXISTS short-circuit | 2–10× |
| 10 | Tracking vs NoTracking | `AsNoTracking` + DTO projection | 1.5–3× |
| 11 | Over-Indexed Table | Remove redundant indexes (11 → 3 B-tree updates/write) | 2–5× |
| 12 | Random vs Sequential GUID PK | `Guid.CreateVersion7()` (time-ordered) | 1.5–3× |

---

## Scenario 11 — Over-Indexed Table

### The Problem

A `ProductReviews` table that has grown too many indexes over time — each added to
speed up a specific read query but collectively making all writes significantly slower.

### Naive Approach — `NaiveOverIndexedReviewQueries` (DbNaive – 10 indexes)

```csharp
// ❌ Application code is identical to optimized.
// The anti-pattern is ENTIRELY in the schema: 10 non-clustered indexes.
// Each INSERT/UPDATE/DELETE must maintain 11 B-tree structures
// (1 clustered PK + 10 non-clustered).
context.ProductReviews.AddRange(reviews);
await context.SaveChangesAsync();
```

**What goes wrong:**

Every non-clustered index is a separate B-tree stored alongside the data. Every
INSERT must find the right position in **all** 10 B-trees and write a new entry.
Several of the 10 indexes are **redundant or overlapping**:

| Index | Problem |
|-------|---------|
| `(ProductId)` | Superseded by `(ProductId, Rating)` and `(ProductId, CreatedAt)` |
| `(Rating)` | Only 5 distinct values — near-useless (high scan, low elimination) |
| `(IsVerifiedPurchase)` | Only 2 distinct values — completely useless index |
| `(ProductId, Rating)` + `(ProductId, CreatedAt)` | Overlap each other and `(ProductId)` |
| `(Rating, IsVerifiedPurchase)` | Overlaps `(Rating)` and `(IsVerifiedPurchase)` |
| `(Title)` | Long text key, high cardinality but rarely searched alone |

### Optimized Approach — `OptimizedOverIndexedReviewQueries` (DbOptimized – 2 indexes)

```csharp
// ✅ Same application code. The optimization is the schema:
// Only 2 targeted covering indexes — 3 total B-tree updates per write.

// IX_Reviews_ProductId_Rating (ProductId, Rating DESC)
//    INCLUDE (CustomerId, Title, CreatedAt, IsVerifiedPurchase, HelpfulVotes)
//    → Covers: WHERE ProductId = ? AND Rating >= ? ORDER BY Rating DESC
//
// IX_Reviews_CustomerId_CreatedAt (CustomerId, CreatedAt DESC)
//    INCLUDE (ProductId, Rating, Title)
//    → Covers: WHERE CustomerId = ? ORDER BY CreatedAt DESC
context.ProductReviews.AddRange(reviews);
await context.SaveChangesAsync();
```

**Why it’s better:**

| Aspect | Naive (10 indexes) | Optimized (2 indexes) |
|--------|--------------------|-----------------------|
| B-tree updates per INSERT | 11 (1 clustered + 10 NCI) | 3 (1 clustered + 2 NCI) |
| B-tree updates per UPDATE | Up to 11 (all affected indexes) | Up to 3 |
| Storage overhead | ~10 index pages per data page | ~2 index pages per data page |
| Query optimizer clarity | Confused by redundant paths | Single unambiguous covering index |
| Application code change | None needed | None needed |

**Expected gain: 2×–5× faster writes**

> **Key lesson:** The optimizer picks the best available index for reads regardless
> of how many you have. But writes pay the cost for ALL indexes, used or not.
> Removing an unused index is free read performance maintained and free write speed gained.

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| INSERT: both tables accept the same rows | Correctness | `OverIndexedReviewTests.cs` | 20 rows seeded = 20 rows found in both DBs |
| READ: GetTopRated returns count from both tables | Correctness | `OverIndexedReviewTests.cs` | Both tables are readable |
| READ: all results satisfy minRating filter | Correctness | `OverIndexedReviewTests.cs` | No incorrect rows returned |
| UPDATE: HelpfulVotes increment works on both tables | Correctness | `OverIndexedReviewTests.cs` | Same update semantics regardless of schema |
| INSERT: properly-indexed faster than over-indexed | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: INSERT + UPDATE (4 benchmarks) | Benchmark | `OverIndexedBenchmark.cs` | Naive INSERT, Optimized INSERT, Naive UPDATE, Optimized UPDATE |

---

## Scenario 12 — Random vs Sequential GUID PK

### The Problem

All write-heavy tables that use a `UNIQUEIDENTIFIER` (GUID) clustered primary key generated
by `Guid.NewGuid()` suffer from **clustered index fragmentation**. SQL Server stores rows in
PK order inside the clustered B-tree. A random GUID is statistically unlikely to be the
largest existing key, so SQL Server must:

1. Find the page where the new GUID belongs (somewhere in the middle of the B-tree).
2. If the page is full (~80% fill factor), **split it** into two half-full pages.
3. Write the new row to the correct half.

With 1 000 inserts, roughly **500 page splits** occur. Each split writes two pages instead
of one, doubling I/O. Over time the index becomes severely fragmented.

### Naive Approach — `NaiveGuidKeyQueries` (`Guid.NewGuid()`)

```csharp
// ❌ Random GUID → insert at a random position in the clustered B-tree
//    Expected: ~50% page-split rate per INSERT batch
var log = new AuditLog
{
    Id = Guid.NewGuid(),  // ❌ random: e.g. {3f7a21cb-...}
    ...
};
context.AuditLogs.Add(log);
await context.SaveChangesAsync();
```

**What goes wrong:**

| Symptom | Cause |
|---------|-------|
| ~50 % page splits per batch | New GUID is almost never the largest |
| Pages fill to ~50 % after splits | Each split produces two half-full pages |
| Growing storage footprint | Fragmented pages waste disk space |
| Query scans read twice as many pages | Reads are slower too, not just writes |
| Fragmentation compounds over time | Without `REBUILD INDEX` it never recovers |

### Optimized Approach — `OptimizedGuidKeyQueries` (`Guid.CreateVersion7()`)

```csharp
// ✅ Version 7 UUID: high bits = Unix ms timestamp → monotonically increasing
//    New GUID is always > all previously generated GUIDs → always appends to end
var log = new AuditLog
{
    Id = Guid.CreateVersion7(),  // ✅ time-ordered: e.g. {019504d8-...}
    ...
};
context.AuditLogs.Add(log);
await context.SaveChangesAsync();
```

**Why it’s better:**

| Aspect | Naive (`Guid.NewGuid()`) | Optimized (`Guid.CreateVersion7()`) |
|--------|--------------------------|--------------------------------------|
| Page-split rate | ~50 % | ~0 % |
| Page fill factor | ~50 % after fragmentation | ~80 % (default fill factor, maintained) |
| Write I/O per row | 2× (split writes 2 pages) | 1× (always appends to last page) |
| Index fragmentation over time | Accumulates → requires `REBUILD INDEX` | Negligible |
| Global uniqueness | ✅ | ✅ (timestamp prefix + random suffix) |
| Code change required | None | One word: `NewGuid()` → `CreateVersion7()` |

**Expected gain: 1.5×–3× faster batch INSERTs; gap widens as table grows**

> **Key lesson:** Never use `Guid.NewGuid()` as a clustered B-tree key for write-heavy tables.
> If you need globally unique IDs for external APIs, use `Guid.CreateVersion7()` (time-ordered)
> or an `INT IDENTITY` with a separate `UNIQUEIDENTIFIER` column for public exposure.

### Tests

| Test | Type | File | What It Validates |
|------|------|------|-------------------|
| INSERT: both tables accept the same rows | Correctness | `GuidKeyAuditLogTests.cs` | 20 rows seeded = 20 rows found in both DBs |
| READ: GetRecentByEntity returns rows from both | Correctness | `GuidKeyAuditLogTests.cs` | Both tables are readable |
| READ: retrieved entries have valid non-empty GUIDs | Correctness | `GuidKeyAuditLogTests.cs` | No zero-GUID IDs returned |
| READ: sequential GUIDs (v7) sort chronologically | Correctness | `GuidKeyAuditLogTests.cs` | `Guid.CreateVersion7()` is monotonically increasing |
| INSERT: sequential GUIDs faster than random GUIDs | Performance | `PerformanceAssertionTests.cs` | Ratio guard |
| BenchmarkDotNet: INSERT (2 benchmarks) | Benchmark | `GuidKeyBenchmark.cs` | NaiveInsert (random), OptimizedInsert (sequential) |
