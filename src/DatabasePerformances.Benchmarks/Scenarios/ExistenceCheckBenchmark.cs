using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 9 — Count() vs Any() for Existence Checks
/// ============================================================
/// Compares checking row existence using <c>Count() > 0</c>
/// (forces a full COUNT aggregate) vs <c>Any()</c> (EXISTS
/// short-circuit — stops at first match).
///
/// Naive:     <c>Count() > 0</c>  → scans/counts all matching rows.
/// Optimized: <c>Any()</c>        → EXISTS, stops at first row.
///
/// Expected ratio: 2×–10× faster for optimized on large tables,
/// because Any can stop scanning after the first matching row.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("ExistenceCheck")]
public class ExistenceCheckBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveExistenceCheckQueries _naive = null!;
    private OptimizedExistenceCheckQueries _optimized = null!;

    // Use a customer ID that has orders so both queries return true
    private const int TestCustomerId = 1;

    [GlobalSetup]
    public async Task Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveExistenceCheckQueries(_naiveCtx);
        _optimized    = new OptimizedExistenceCheckQueries(_optimizedCtx);

        // Pre-fetch an existing email for the email-existence test
        var customer = await _optimizedCtx.Customers.FindAsync(TestCustomerId);
        _testEmail = customer?.Email ?? "john@example.com";
    }

    private string _testEmail = null!;

    /// <summary>
    /// ❌ Count() > 0 on Customers.Email — counts all matches before comparing.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — Count() > 0 email existence")]
    public async Task<bool> NaiveCountEmail()
        => await _naive.CustomerExistsByEmailAsync(_testEmail);

    /// <summary>
    /// ✅ Any() on Customers.Email — EXISTS short-circuit + index seek.
    /// </summary>
    [Benchmark(Description = "Optimized — Any() email existence (EXISTS)")]
    public async Task<bool> OptimizedAnyEmail()
        => await _optimized.CustomerExistsByEmailAsync(_testEmail);

    /// <summary>
    /// ❌ Count() > 0 on Orders.CustomerId — counts ALL orders for the customer.
    /// </summary>
    [Benchmark(Description = "Naive — Count() > 0 has-orders check")]
    public async Task<bool> NaiveCountOrders()
        => await _naive.CustomerHasOrdersAsync(TestCustomerId);

    /// <summary>
    /// ✅ Any() on Orders.CustomerId — EXISTS with index seek, stops at first row.
    /// </summary>
    [Benchmark(Description = "Optimized — Any() has-orders check (EXISTS)")]
    public async Task<bool> OptimizedAnyOrders()
        => await _optimized.CustomerHasOrdersAsync(TestCustomerId);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
