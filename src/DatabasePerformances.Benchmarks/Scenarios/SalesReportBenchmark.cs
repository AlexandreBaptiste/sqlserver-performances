using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 4 — Top Products by Revenue (last 30 days)
/// ============================================================
/// Aggregates OrderItems over 3M rows to find the 10 best-selling products.
///
/// Naive:     Row-store scan of entire OrderItems table, no date index.
/// Optimized: Columnstore index (batch-mode execution) + IX_Orders_OrderDate
///            for date pruning + Dapper to skip EF materialisation overhead.
///
/// Expected ratio: 5× – 50× faster (highly dependent on data distribution).
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("SalesReport")]
public class SalesReportBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveSalesReportQueries _naive = null!;
    private OptimizedSalesReportQueries _optimized = null!;

    private static readonly DateTime _from = DateTime.UtcNow.AddDays(-30);
    private static readonly DateTime _to   = DateTime.UtcNow;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveSalesReportQueries(_naiveCtx);
        _optimized    = new OptimizedSalesReportQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ Row-store aggregation over 3M rows, no date/FK index, no columnstore.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — row-store full scan aggregation")]
    public async Task NaiveTopProducts()
        => await _naive.GetTopProductsByRevenueAsync(_from, _to);

    /// <summary>
    /// ✅ Columnstore batch-mode + date seek + Dapper (no EF materialisation).
    /// </summary>
    [Benchmark(Description = "Optimized — columnstore + Dapper")]
    public async Task OptimizedTopProducts()
        => await _optimized.GetTopProductsByRevenueAsync(_from, _to);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
