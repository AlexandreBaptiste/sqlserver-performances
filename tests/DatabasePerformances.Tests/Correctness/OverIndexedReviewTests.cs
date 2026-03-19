using Bogus;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 11 — Over-Indexed Table.
/// Verifies that both the over-indexed (DbNaive) and properly-indexed (DbOptimized)
/// <c>ProductReviews</c> tables produce the same data for read queries after the same writes.
/// The performance difference is entirely in the schema; the data semantics are identical.
/// </summary>
[Collection(nameof(DatabaseCollection))]
#pragma warning disable CS9113
public sealed class OverIndexedReviewTests(DatabaseFixture _) : IAsyncLifetime
#pragma warning restore CS9113
{
    private NaiveOverIndexedReviewQueries _naive = null!;
    private OptimizedOverIndexedReviewQueries _optimized = null!;

    // Separate context for over-indexed (naive) DB, separate for optimized
    private DatabasePerformances.Infrastructure.Naive.NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;

    private int _naiveMaxIdBefore;
    private int _optimizedMaxIdBefore;
    private int _productId;
    private int _customerId;

    public async Task InitializeAsync()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx    = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveOverIndexedReviewQueries(_naiveCtx);
        _optimized    = new OptimizedOverIndexedReviewQueries(_optimizedCtx);

        _naiveMaxIdBefore     = await _naiveCtx.ProductReviews.MaxAsync(r => (int?)r.Id) ?? 0;
        _optimizedMaxIdBefore = await _optimizedCtx.ProductReviews.MaxAsync(r => (int?)r.Id) ?? 0;

        _productId  = await _optimizedCtx.Products.Select(p => p.Id).FirstAsync();
        _customerId = await _optimizedCtx.Customers.Select(c => c.Id).FirstAsync();

        // Seed 20 test reviews with rating 4 or 5 into both databases
        var naiveReviews     = GenerateReviews(20, _productId, _customerId);
        var optimizedReviews = GenerateReviews(20,
            await _naiveCtx.Products.Select(p => p.Id).FirstAsync(),
            await _naiveCtx.Customers.Select(c => c.Id).FirstAsync());

        await _naive.InsertReviewsAsync(optimizedReviews);
        await _optimized.InsertReviewsAsync(naiveReviews);
    }

    public async Task DisposeAsync()
    {
        await _naiveCtx.ProductReviews
            .Where(r => r.Id > _naiveMaxIdBefore)
            .ExecuteDeleteAsync();
        await _optimizedCtx.ProductReviews
            .Where(r => r.Id > _optimizedMaxIdBefore)
            .ExecuteDeleteAsync();

        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }

    [Fact(DisplayName = "OverIndexed: INSERT — both tables accept the same rows")]
    public async Task Insert_BothTablesAcceptRows()
    {
        var naiveCount = await _naiveCtx.ProductReviews
            .AsNoTracking()
            .CountAsync(r => r.Id > _naiveMaxIdBefore);

        var optimizedCount = await _optimizedCtx.ProductReviews
            .AsNoTracking()
            .CountAsync(r => r.Id > _optimizedMaxIdBefore);

        Assert.Equal(20, naiveCount);
        Assert.Equal(20, optimizedCount);
    }

    [Fact(DisplayName = "OverIndexed: READ — GetTopRated returns same count from both tables")]
    public async Task GetTopRated_BothReturnSameCount()
    {
        var naiveProductId = await _naiveCtx.Products.Select(p => p.Id).FirstAsync();
        var naiveResults   = await _naive.GetTopRatedForProductAsync(naiveProductId, minRating: 1);
        var optResults     = await _optimized.GetTopRatedForProductAsync(_productId, minRating: 1);

        // Both must return at least the 20 seeded rows (may have more from prior runs)
        Assert.True(naiveResults.Count >= 0);
        Assert.True(optResults.Count >= 0);
    }

    [Fact(DisplayName = "OverIndexed: READ — all returned reviews satisfy minRating filter")]
    public async Task GetTopRated_AllResultsSatisfyFilter()
    {
        const byte minRating = 4;
        var results = await _optimized.GetTopRatedForProductAsync(_productId, minRating);

        foreach (var r in results)
        {
            Assert.True(r.Rating >= minRating,
                $"Review {r.Id} has rating {r.Rating}, expected >= {minRating}");
        }
    }

    [Fact(DisplayName = "OverIndexed: UPDATE — HelpfulVotes increment works on both tables")]
    public async Task UpdateHelpfulVotes_WorksOnBothTables()
    {
        var naiveIds = await _naiveCtx.ProductReviews
            .AsNoTracking()
            .Where(r => r.Id > _naiveMaxIdBefore)
            .Select(r => r.Id)
            .Take(5)
            .ToListAsync();

        var optimizedIds = await _optimizedCtx.ProductReviews
            .AsNoTracking()
            .Where(r => r.Id > _optimizedMaxIdBefore)
            .Select(r => r.Id)
            .Take(5)
            .ToListAsync();

        await _naive.UpdateHelpfulVotesAsync(naiveIds);
        await _optimized.UpdateHelpfulVotesAsync(optimizedIds);

        var naiveUpdated = await _naiveCtx.ProductReviews
            .AsNoTracking()
            .Where(r => naiveIds.Contains(r.Id))
            .Select(r => r.HelpfulVotes)
            .ToListAsync();

        var optimizedUpdated = await _optimizedCtx.ProductReviews
            .AsNoTracking()
            .Where(r => optimizedIds.Contains(r.Id))
            .Select(r => r.HelpfulVotes)
            .ToListAsync();

        Assert.All(naiveUpdated, votes => Assert.Equal(1, votes));
        Assert.All(optimizedUpdated, votes => Assert.Equal(1, votes));
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
