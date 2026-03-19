using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 5 — N+1 Query Problem
/// ============================================================
/// Loads 100 orders and their line items.
///
/// Naive:     1 query for orders + 1 query PER order for items = 101 queries.
/// Optimized: AsSplitQuery() — exactly 2 queries total regardless of order count.
///
/// Expected ratio: 20× – 100× faster (each extra round-trip adds ~5-50ms).
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("NPlusOne")]
public class NPlusOneBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveOrderQueries _naive = null!;
    private OptimizedOrderQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveOrderQueries(_naiveCtx);
        _optimized    = new OptimizedOrderQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ 101 separate database queries — 1 for orders + 1 per order for items.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — N+1 problem (101 queries for 100 orders)")]
    public async Task NaiveNPlusOne()
        => await _naive.GetOrdersWithItemsNPlusOneAsync(take: 100);

    /// <summary>
    /// ✅ 2 queries total — AsSplitQuery() fetches all items in a single batched query.
    /// </summary>
    [Benchmark(Description = "Optimized — AsSplitQuery (2 queries for 100 orders)")]
    public async Task OptimizedAsSplitQuery()
        => await _optimized.GetOrdersWithItemsAsync(take: 100);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
