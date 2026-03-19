using BenchmarkDotNet.Attributes;
using Bogus;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 11 — Over-Indexed Table: Write Overhead
/// ============================================================
/// Compares INSERT and UPDATE performance on two identical tables
/// whose only difference is the number of non-clustered indexes:
///
/// Naive:     10 indexes (redundant + overlapping) → 11 B-tree updates per write
/// Optimized: 2 targeted covering indexes          →  3 B-tree updates per write
///
/// The application code is <b>identical</b> for both — the entire performance
/// difference is in the database schema design.
///
/// Key insight: every index you add to a table is a tax on ALL future writes.
/// Indexes that are never used (or whose coverage is superseded by another
/// index) provide zero read benefit while continuously degrading write performance.
///
/// Expected ratio: 2×–5× slower inserts and updates on the over-indexed table.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("OverIndexed")]
public class OverIndexedBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveOverIndexedReviewQueries _naive = null!;
    private OptimizedOverIndexedReviewQueries _optimized = null!;

    // IDs that exist in both databases (seeded data starts at 1)
    private int _naiveProductId;
    private int _naiveCustomerId;
    private int _optimizedProductId;
    private int _optimizedCustomerId;

    // Baseline max IDs so IterationCleanup only removes benchmark-inserted rows
    private int _naiveMaxIdBefore;
    private int _optimizedMaxIdBefore;

    // IDs of pre-seeded reviews used for the UPDATE benchmarks
    private List<int> _naiveUpdateIds = null!;
    private List<int> _optimizedUpdateIds = null!;

    private List<ProductReview> _naiveInsertBatch = null!;
    private List<ProductReview> _optimizedInsertBatch = null!;

    private const int BatchSize = 1_000;

    [GlobalSetup]
    public async Task Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveOverIndexedReviewQueries(_naiveCtx);
        _optimized    = new OptimizedOverIndexedReviewQueries(_optimizedCtx);

        // Grab the first existing product and customer IDs from each database
        _naiveProductId     = await _naiveCtx.Products.Select(p => p.Id).FirstAsync();
        _naiveCustomerId    = await _naiveCtx.Customers.Select(c => c.Id).FirstAsync();
        _optimizedProductId = await _optimizedCtx.Products.Select(p => p.Id).FirstAsync();
        _optimizedCustomerId = await _optimizedCtx.Customers.Select(c => c.Id).FirstAsync();

        // Capture baseline max IDs (for safe cleanup)
        _naiveMaxIdBefore     = await _naiveCtx.ProductReviews.MaxAsync(r => (int?)r.Id) ?? 0;
        _optimizedMaxIdBefore = await _optimizedCtx.ProductReviews.MaxAsync(r => (int?)r.Id) ?? 0;

        // Pre-seed 1000 reviews in each database for the UPDATE benchmarks
        var naiveSeed = GenerateReviews(BatchSize, _naiveProductId, _naiveCustomerId);
        var optimizedSeed = GenerateReviews(BatchSize, _optimizedProductId, _optimizedCustomerId);

        await _naive.InsertReviewsAsync(naiveSeed);
        await _optimized.InsertReviewsAsync(optimizedSeed);

        _naiveUpdateIds = await _naiveCtx.ProductReviews
            .Where(r => r.Id > _naiveMaxIdBefore).Select(r => r.Id).ToListAsync();
        _optimizedUpdateIds = await _optimizedCtx.ProductReviews
            .Where(r => r.Id > _optimizedMaxIdBefore).Select(r => r.Id).ToListAsync();

        // Update the baselines so IterationCleanup won't delete the pre-seeded rows
        _naiveMaxIdBefore     = _naiveUpdateIds.Max();
        _optimizedMaxIdBefore = _optimizedUpdateIds.Max();
    }

    [IterationSetup]
    public void GenerateInsertBatches()
    {
        // Fresh entities each iteration — IDs reset to 0 so EF Core treats them as new rows
        _naiveInsertBatch     = GenerateReviews(BatchSize, _naiveProductId, _naiveCustomerId);
        _optimizedInsertBatch = GenerateReviews(BatchSize, _optimizedProductId, _optimizedCustomerId);
    }

    [IterationCleanup]
    public async Task DeleteInsertedRows()
    {
        await _naiveCtx.ProductReviews
            .Where(r => r.Id > _naiveMaxIdBefore)
            .ExecuteDeleteAsync();

        await _optimizedCtx.ProductReviews
            .Where(r => r.Id > _optimizedMaxIdBefore)
            .ExecuteDeleteAsync();
    }

    // -----------------------------------------------------------------------
    // INSERT benchmarks
    // -----------------------------------------------------------------------

    /// <summary>
    /// ❌ INSERT 1000 reviews into a table with 10 non-clustered indexes.
    ///    Each row requires 11 B-tree page updates.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — INSERT 1k reviews (10 indexes, 11 B-tree updates/row)")]
    public async Task NaiveInsert()
        => await _naive.InsertReviewsAsync(_naiveInsertBatch);

    /// <summary>
    /// ✅ INSERT 1000 reviews into a table with 2 non-clustered indexes.
    ///    Each row requires only 3 B-tree page updates.
    /// </summary>
    [Benchmark(Description = "Optimized — INSERT 1k reviews (2 indexes, 3 B-tree updates/row)")]
    public async Task OptimizedInsert()
        => await _optimized.InsertReviewsAsync(_optimizedInsertBatch);

    // -----------------------------------------------------------------------
    // UPDATE benchmarks
    // -----------------------------------------------------------------------

    /// <summary>
    /// ❌ UPDATE HelpfulVotes on 1000 rows in the over-indexed table.
    ///    SQL Server must update all index pages that contain this column.
    /// </summary>
    [Benchmark(Description = "Naive — UPDATE 1k HelpfulVotes (10 indexes to maintain)")]
    public async Task NaiveUpdate()
        => await _naive.UpdateHelpfulVotesAsync(_naiveUpdateIds);

    /// <summary>
    /// ✅ Same UPDATE on the properly-indexed table.
    ///    Only 2 index pages to update per row.
    /// </summary>
    [Benchmark(Description = "Optimized — UPDATE 1k HelpfulVotes (2 indexes to maintain)")]
    public async Task OptimizedUpdate()
        => await _optimized.UpdateHelpfulVotesAsync(_optimizedUpdateIds);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        // Remove pre-seeded update rows
        await _naiveCtx.ProductReviews
            .Where(r => _naiveUpdateIds.Contains(r.Id))
            .ExecuteDeleteAsync();
        await _optimizedCtx.ProductReviews
            .Where(r => _optimizedUpdateIds.Contains(r.Id))
            .ExecuteDeleteAsync();

        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }

    private static List<ProductReview> GenerateReviews(int count, int productId, int customerId)
        => new Faker<ProductReview>("en")
            .RuleFor(r => r.ProductId, _ => productId)
            .RuleFor(r => r.CustomerId, _ => customerId)
            .RuleFor(r => r.Rating, f => f.Random.Byte(1, 5))
            .RuleFor(r => r.Title, f => f.Lorem.Sentence(4))
            .RuleFor(r => r.Body, f => f.Lorem.Paragraph())
            .RuleFor(r => r.CreatedAt, f => f.Date.Past(2))
            .RuleFor(r => r.HelpfulVotes, _ => 0)
            .RuleFor(r => r.IsVerifiedPurchase, f => f.Random.Bool())
            .Generate(count);
}
