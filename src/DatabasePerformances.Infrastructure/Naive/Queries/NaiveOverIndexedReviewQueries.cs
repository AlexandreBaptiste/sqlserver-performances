using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE product review queries — operates on a table with 10 indexes.
///
/// Anti-pattern demonstrated (Scenario 11):
///   Every INSERT, UPDATE, and DELETE on <c>ProductReviews</c> in DbNaive
///   must maintain 10 non-clustered index B-trees in addition to the clustered
///   index.  This means each write operation performs roughly 11× the I/O work
///   compared to a table with 2 well-chosen indexes.
///
///   The application code is <b>identical</b> to the optimized version — the
///   entire performance difference comes from the database schema design:
///   <list type="bullet">
///     <item>11 total indexes to maintain per write (1 clustered + 10 non-clustered)</item>
///     <item>Several indexes overlap or are redundant (e.g. IX_Reviews_ProductId
///           is made redundant by IX_Reviews_ProductId_Rating and
///           IX_Reviews_ProductId_CreatedAt)</item>
///     <item>Low-selectivity indexes (Rating has 5 values; IsVerifiedPurchase
///           has 2) consume space and write overhead without meaningful read gain</item>
///   </list>
/// </summary>
public sealed class NaiveOverIndexedReviewQueries(NaiveDbContext context)
{
    /// <summary>
    /// Inserts <paramref name="reviews"/> into <c>ProductReviews</c> (over-indexed table).
    /// <para>
    /// ❌ Each INSERT maintains 10 non-clustered indexes + 1 clustered index.<br/>
    /// Compare with optimized: only 2 non-clustered + 1 clustered.
    /// </para>
    /// </summary>
    public async Task InsertReviewsAsync(
        IReadOnlyList<ProductReview> reviews,
        CancellationToken cancellationToken = default)
    {
        // ❌ Identical AddRange + SaveChanges code to optimized — the bottleneck is
        //    the schema: 10 indexes must be updated per inserted row, not the ORM.
        context.ProductReviews.AddRange(reviews);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Increments <c>HelpfulVotes</c> for the supplied review IDs.
    /// <para>
    /// ❌ SQL Server must update every index that has <c>HelpfulVotes</c> as a
    /// key or include column (IX_Reviews_HelpfulVotes) for each updated row.
    /// </para>
    /// </summary>
    public async Task UpdateHelpfulVotesAsync(
        IReadOnlyList<int> reviewIds,
        CancellationToken cancellationToken = default)
    {
        // ❌ UPDATE on over-indexed table: each row update touches all index pages
        //    that contain the changed column.
        await context.ProductReviews
            .Where(r => reviewIds.Contains(r.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.HelpfulVotes, r => r.HelpfulVotes + 1),
                cancellationToken);
    }

    /// <summary>Top-rated reviews for a product — demonstrates a read on the over-indexed table.</summary>
    public async Task<List<ProductReviewSummary>> GetTopRatedForProductAsync(
        int productId,
        byte minRating = 4,
        CancellationToken cancellationToken = default)
    {
        // SQL Server may struggle to choose between IX_Reviews_ProductId,
        // IX_Reviews_ProductId_Rating, and IX_Reviews_ProductId_CreatedAt — all overlap.
        return await context.ProductReviews
            .Where(r => r.ProductId == productId && r.Rating >= minRating)
            .OrderByDescending(r => r.Rating)
            .Select(r => new ProductReviewSummary(
                r.Id, r.Title, r.Rating, r.CreatedAt, r.IsVerifiedPurchase, r.HelpfulVotes))
            .ToListAsync(cancellationToken);
    }
}
