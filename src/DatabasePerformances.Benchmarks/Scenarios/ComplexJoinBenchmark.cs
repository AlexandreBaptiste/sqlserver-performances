using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 7 — Complex Join (Order + Customer + Lines + Products + Category)
/// ============================================================
/// Loads the most recent 50 orders with their full details.
///
/// Naive:     No AsSplitQuery → Cartesian product result set; loads
///            Description (NVARCHAR(MAX)); no FK indexes for JOIN lookup.
/// Optimized: AsSplitQuery → 3 focused queries; no Description loaded;
///            FK indexes (IX_OrderItems_OrderId, IX_Orders_OrderDate) used.
///
/// Expected ratio: 5× – 20× faster, significantly lower allocations.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("ComplexJoin")]
public class ComplexJoinBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveComplexJoinQueries _naive = null!;
    private OptimizedComplexJoinQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveComplexJoinQueries(_naiveCtx);
        _optimized    = new OptimizedComplexJoinQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ Cartesian JOIN product; Description column wasted; no FK indexes.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — Cartesian JOIN, SELECT *, no FK indexes")]
    public async Task NaiveComplexJoin()
        => await _naive.GetRecentOrdersWithDetailsAsync(take: 50);

    /// <summary>
    /// ✅ AsSplitQuery + FK index seeks + projection (no Description).
    /// </summary>
    [Benchmark(Description = "Optimized — AsSplitQuery + FK indexes + projection")]
    public async Task OptimizedComplexJoin()
        => await _optimized.GetRecentOrdersWithDetailsAsync(take: 50);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
