using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 10 — Tracking vs No-Tracking for Read-Heavy Workloads
/// ============================================================
/// Loading 5 000 entities with change-tracking enabled vs.
/// <c>AsNoTracking()</c> + lightweight DTO projection.
///
/// Naive:     Default tracking   → snapshot, identity map, full entity.
/// Optimized: AsNoTracking + DTO → no tracker overhead, minimal columns.
///
/// Expected ratio: 1.5× – 3× faster for optimized, with significantly
/// lower memory allocation (measured by MemoryDiagnoser).
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("Tracking")]
public class TrackingBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveTrackingQueries _naive = null!;
    private OptimizedTrackingQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveTrackingQueries(_naiveCtx);
        _optimized    = new OptimizedTrackingQueries(_optimizedCtx);
    }

    /// <summary>
    /// ❌ Loads 5 000 Customer entities with full change-tracking.
    ///   — Snapshot arrays, identity maps, state entries for each entity.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — 5k customers WITH tracking (SELECT *)")]
    public async Task NaiveTracked()
    {
        // Clear the tracker between iterations to avoid accumulation
        _naiveCtx.ChangeTracker.Clear();
        await _naive.GetCustomersTrackedAsync(take: 5000);
    }

    /// <summary>
    /// ✅ Loads 5 000 customers as DTOs with AsNoTracking.
    ///   — No snapshots, no identity maps, lighter memory footprint.
    /// </summary>
    [Benchmark(Description = "Optimized — 5k customers NO tracking (DTO projection)")]
    public async Task OptimizedNoTracking()
        => await _optimized.GetCustomersNoTrackingAsync(take: 5000);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
