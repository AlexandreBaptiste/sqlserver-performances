using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 2 — Order History per Customer
/// ============================================================
/// Fetches all orders for a single customer.
///
/// Naive:     No index on Orders.CustomerId → full scan of 1M-row table.
/// Optimized: IX_Orders_CustomerId index seek → directly jumps to the rows.
///
/// Expected ratio: 20× – 100× faster for optimized on deep-middle customers.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("OrderHistory")]
public class OrderHistoryBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveOrderQueries _naive = null!;
    private OptimizedOrderQueries _optimized = null!;

    // A customer ID that exists — seeded data starts at 1
    private const int TestCustomerId = 1000;

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
    /// ❌ Full scan on 1M Orders rows — no FK index on CustomerId.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — full table scan on Orders")]
    public async Task NaiveOrderHistory()
        => await _naive.GetOrdersByCustomerAsync(TestCustomerId);

    /// <summary>
    /// ✅ Index seek via IX_Orders_CustomerId with covering INCLUDE columns.
    /// </summary>
    [Benchmark(Description = "Optimized — index seek + projection")]
    public async Task OptimizedOrderHistory()
        => await _optimized.GetOrdersByCustomerAsync(TestCustomerId);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
