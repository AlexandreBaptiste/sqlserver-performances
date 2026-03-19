using BenchmarkDotNet.Attributes;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 6 — Deep Pagination
/// ============================================================
/// Retrieves page 50 000 of orders (i.e., there are ~50 000 pages before it).
///
/// Naive:     OFFSET 999 980 ROWS FETCH NEXT 20 — SQL Server must generate
///            and throw away ~1 000 000 rows for every request.
/// Optimized: Keyset pagination WHERE Id &gt; @cursor — O(1) index seek,
///            cost is the same for page 1 and page 1 000 000.
///
/// Expected ratio: 50× – 500× faster at deep pages.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("Pagination")]
public class PaginationBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaivePaginationQueries _naive = null!;
    private OptimizedPaginationQueries _optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaivePaginationQueries(_naiveCtx);
        _optimized    = new OptimizedPaginationQueries(_optimizedCtx);
    }

    // Simulate a deep page request — page 50 000 × 20 rows = offset ~1M
    private const int DeepPageNumber = 50_000;
    // For keyset, we simulate being at the same position: cursor at row ~1M
    private const int KeysetCursor = 999_980;

    /// <summary>
    /// ❌ OFFSET 999 980 ROWS — SQL Server builds a worktable with 1M rows
    /// then discards all but the last 20.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — OFFSET/FETCH deep page (1M rows discarded)")]
    public async Task NaiveOffsetPagination()
        => await _naive.GetOrderPageAsync(pageNumber: DeepPageNumber, pageSize: 20);

    /// <summary>
    /// ✅ Keyset pagination — WHERE Id &gt; 999980 ORDER BY Id FETCH 20
    /// — single index seek, same cost at any depth.
    /// </summary>
    [Benchmark(Description = "Optimized — Keyset pagination (O(1) seek)")]
    public async Task OptimizedKeysetPagination()
        => await _optimized.GetOrderPageAsync(lastSeenId: KeysetCursor, pageSize: 20);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
