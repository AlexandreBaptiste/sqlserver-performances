using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 1 — Customer Search
/// ============================================================
/// Compares searching for customers by email/name between the
/// two databases and two query patterns.
///
/// Naive:     LIKE '%term%'   — no index, full table scan (200k rows)
/// Optimized: LIKE 'term%'   — prefix match with IX_Customers_Email seek
///
/// Expected ratio: 10× – 50× faster for optimized.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("CustomerSearch")]
public class CustomerSearchBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveCustomerQueries _naive = null!;
    private OptimizedCustomerQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveCustomerQueries(_naiveCtx);
        _optimized    = new OptimizedCustomerQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ Full table scan — LIKE '%Joh%' on 200k rows with no index.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — LIKE '%term%' full scan")]
    public async Task NaiveEmailSearch()
        => await _naive.SearchByEmailAsync("gmail");

    /// <summary>
    /// ✅ Index seek — LIKE 'Joh%' using IX_Customers_Email.
    /// </summary>
    [Benchmark(Description = "Optimized — LIKE 'term%' index seek + projection")]
    public async Task OptimizedEmailSearch()
        => await _optimized.SearchByEmailPrefixAsync("jo");

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
