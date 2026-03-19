# Database Performance Benchmarks — .NET 10

An educational .NET 10 solution that demonstrates **real-world database performance issues** and their solutions using SQL Server 2022, Entity Framework Core, and Dapper.

Each scenario pairs a ❌ **naive** implementation against a ✅ **optimized** one and measures the difference with BenchmarkDotNet.

---

## 📐 Architecture

```
database-performances/
├── docker/
│   ├── docker-compose.yml           # SQL Server 2022 + init container
│   └── sql/
│       ├── 00-create-databases.sql  # Creates DbNaive and DbOptimized
│       ├── 01-schema-naive.sql      # Tables with PK only (no extra indexes)
│       └── 02-schema-optimized.sql  # Same tables + all optimized indexes
├── src/
│   ├── DatabasePerformances.Domain/          # POCO entities
│   ├── DatabasePerformances.Infrastructure/  # EF Core contexts, Dapper queries
│   ├── DatabasePerformances.Seeder/          # Bogus + SqlBulkCopy data generator
│   └── DatabasePerformances.Benchmarks/      # BenchmarkDotNet console app
├── tests/
│   └── DatabasePerformances.Tests/           # xUnit correctness + perf assertions
└── results/
    └── benchmark-results.md                  # Auto-generated BenchmarkDotNet output
```

### Two databases, one schema

| Database | Indexes |
|---|---|
| `DbNaive` | Primary keys only — every non-PK lookup is a full table scan |
| `DbOptimized` | Full set of composite, covering, filtered, and columnstore indexes |

---

## 🗃️ Domain Model

```
Customer (200k) ──── (1M) Order ──── (3M) OrderItem ──── Product (5k)
    │                                                          │
    └── Address                                           Category (20)
```

---

## 🔬 Benchmark Scenarios

| # | Scenario | Naive anti-pattern | Optimized solution | Expected gain |
|---|---|---|---|---|
| 1 | Customer Search | `LIKE '%term%'` — no index | `LIKE 'term%'` + `IX_Customers_Email` seek | 10–50× |
| 2 | Order History | FK column unindexed → 1M-row scan | `IX_Orders_CustomerId` seek | 20–100× |
| 3 | Product Catalog | No composite index, `SELECT *` with `NVARCHAR(MAX)` | Covering index `(CategoryId,Price) INCLUDE(Name,Stock)` | 3–10× |
| 4 | Sales Report (Top 10) | Row-store full scan on 3M rows | Columnstore index + Dapper + date index  | 5–50× |
| 5 | N+1 Problem | 101 queries for 100 orders | `AsSplitQuery()` — 2 queries total | 20–100× |
| 6 | Deep Pagination | `OFFSET 999980 ROWS` — 1M rows discarded | Keyset `WHERE Id > @cursor` — O(1) | 50–500× |
| 7 | Complex Join | Cartesian product, loads `Description` (MAX) | `AsSplitQuery` + FK index seeks + projection | 5–20× |
| 8 | Bulk Insert (10k rows) | `SaveChanges` per entity — 10k round-trips | `AddRange` + `SqlBulkCopy` | 40–200× |
| 9 | Count() vs Any() | `Count() > 0` scans all matching rows | `Any()` → `EXISTS` short-circuit at first match | 2–10× |
| 10 | Tracking vs NoTracking | Full change-tracking + `SELECT *` on 5k rows | `AsNoTracking()` + DTO projection | 1.5–3× |
| 11 | Over-Indexed Table | 10 redundant indexes → 11 B-tree updates per write | 2 targeted covering indexes → 3 B-tree updates per write | 2–5× |
| 12 | Random vs Sequential GUID PK | `Guid.NewGuid()` clustered PK → page splits & fragmentation | `Guid.CreateVersion7()` → sequential inserts, no page splits | 1.5–3× |

---

## 🚀 Quick Start

### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 2. Start SQL Server

```bash
cd docker
docker compose up -d
```

Wait for the `db-perf-init` container to exit (it runs the schema scripts):

```bash
docker logs db-perf-init
# Expected last line: "Database initialization complete."
```

### 3. Seed the databases

**Quick mode** (~10k customers, completes in ~30 seconds):

```bash
cd src/DatabasePerformances.Seeder
dotnet run -- --quick
```

**Full mode** (~200k customers, ~1M orders, ~3M items — takes ~5–10 minutes):

```bash
dotnet run
```

The seeder is **idempotent** — running it again on an already-seeded database is a no-op.

### 4. Run the tests

```bash
dotnet test
```

Tests verify two things:
- **Correctness**: naive and optimized return the same logical results
- **Performance assertions**: optimized implementations meet time thresholds

> ⚠ Performance tests require the **full** dataset to produce meaningful ratios.

### 5. Run the benchmarks

```bash
cd src/DatabasePerformances.Benchmarks
dotnet run -c Release
```

BenchmarkDotNet will present an interactive menu. Select a scenario or run all.

Results are written to `BenchmarkDotNet.Artifacts/results/` as Markdown and CSV.

**Run a specific scenario:**

```bash
dotnet run -c Release -- --filter *CustomerSearch*
dotnet run -c Release -- --filter *NPlusOne*
dotnet run -c Release -- --filter *Pagination*
```

---

## ⚙️ Configuration

Both the Seeder, Benchmarks, and Tests projects read connection strings from `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "NaiveDatabase":     "Server=localhost,1433;Database=DbNaive;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;",
    "OptimizedDatabase": "Server=localhost,1433;Database=DbOptimized;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  }
}
```

Override any value with an environment variable:

```bash
# Linux / macOS
export ConnectionStrings__NaiveDatabase="Server=myhost,1433;..."

# Windows (PowerShell)
$env:ConnectionStrings__NaiveDatabase="Server=myhost,1433;..."
```

---

## 📊 Reading Benchmark Results

BenchmarkDotNet output includes:

| Column | Meaning |
|---|---|
| **Mean** | Average execution time per operation |
| **Ratio** | Naive is the baseline (1.00); optimized shows how many times faster |
| **Allocated** | Memory allocated per operation (lower is better) |
| **Gen0/1/2** | GC collection pressure |

Example (illustrative — actual numbers depend on hardware):

```
| Method                   |       Mean |  Ratio | Allocated |
|------------------------- |-----------:|-------:|----------:|
| Naive (full scan)        | 4,500.0 ms |   1.00 |   42.3 MB |
| Optimized (index seek)   |    85.0 ms |   0.02 |    1.1 MB |
```

> The `Ratio` column tells you: the optimized version is ~53× faster **and** uses 38× less memory.

---

## 🧑‍🏫 Key Concepts Demonstrated

### Indexes
- **Non-clustered index** — fast lookup for equality/range predicates
- **Composite index** — covers multi-column WHERE clauses in a single seek
- **Covering index** (INCLUDE) — eliminates key lookups entirely
- **Filtered index** — smaller index for a common subset (e.g., non-cancelled orders)
- **Columnstore index** — batch-mode aggregation for analytical queries (10-100× on GROUP BY)

### EF Core Patterns
- `AsNoTracking()` — skip change tracking for read-only queries
- `AsSplitQuery()` — avoid Cartesian products on large Include chains
- `EF.CompileAsyncQuery()` — pre-compile hot-path LINQ expressions
- Projection with `Select()` — fetch only needed columns
- `GroupBy` push-down — let SQL Server aggregate, not C#

### SQL Anti-Patterns  
- `LIKE '%term%'` — leading wildcard disables any index
- Missing FK indexes — every join becomes a full scan
- `OFFSET n ROWS` deep pagination — O(n) cost at every depth
- N+1 queries — the silent killer of ORM-based applications
- `SELECT *` with `NVARCHAR(MAX)` — wastes I/O budget on unused data

---

## 🐳 Docker Commands

```bash
# Start SQL Server
docker compose -f docker/docker-compose.yml up -d

# Stop and keep data
docker compose -f docker/docker-compose.yml stop

# Stop and remove volumes (wipe data)
docker compose -f docker/docker-compose.yml down -v

# Check SQL Server logs
docker logs db-perf-sqlserver

# Connect with sqlcmd
docker exec -it db-perf-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C
```

---

## 📁 Results

Pre-run benchmark results are stored in [results/benchmark-results.md](results/benchmark-results.md).

Run your own benchmarks on your hardware to see results representative of your environment.

## 📖 Test & Optimization Reference

For a deep-dive into every scenario, see **[docs/test-reference.md](docs/test-reference.md)**.

It documents all 10 scenarios side-by-side — what the naive code does wrong, why the optimized version is faster, code snippets for both approaches, comparison tables, and a full mapping of every correctness test (27), performance assertion test (14), and BenchmarkDotNet scenario (22 benchmarks) to the scenario it covers.
