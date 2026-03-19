using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 3 — Product Catalog Filtering
/// ============================================================
/// Filters 5 000 products by category AND price range.
///
/// Naive:     No composite index → two separate scans, full Products table read.
/// Optimized: IX_Products_CategoryId_Price INCLUDE (Name, Stock)
///            → covering index seek, no heap access, no Description column read.
///
/// Expected ratio: 3× – 10× faster, and dramatically lower memory allocations.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("ProductCatalog")]
public class ProductCatalogBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveProductQueries _naive = null!;
    private OptimizedProductQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveProductQueries(_naiveCtx);
        _optimized    = new OptimizedProductQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ Full Products scan, loads Description (NVARCHAR(MAX)) for every row.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — full scan, SELECT *, in-memory filter")]
    public async Task NaiveProductFilter()
        => await _naive.GetByCategoryAndPriceRangeAsync(categoryId: 1, minPrice: 10, maxPrice: 500);

    /// <summary>
    /// ✅ Covering index seek — only Name and Stock fetched per filtered row.
    /// </summary>
    [Benchmark(Description = "Optimized — covering index seek, projection")]
    public async Task OptimizedProductFilter()
        => await _optimized.GetByCategoryAndPriceRangeAsync(categoryId: 1, minPrice: 10, maxPrice: 500);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
