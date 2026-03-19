using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED product review queries — operates on a table with 2 targeted indexes.
///
/// Optimization demonstrated (Scenario 11):
///   <c>ProductReviews</c> in DbOptimized has only 2 non-clustered covering indexes,
///   chosen to match the two actual read patterns:
///   <list type="bullet">
///     <item><b>IX_Reviews_ProductId_Rating</b> — supports filtering by product and rating,
///           with all projected columns in INCLUDE (zero key-lookups).</item>
///     <item><b>IX_Reviews_CustomerId_CreatedAt</b> — supports listing a customer's reviews
///           sorted by date.</item>
///   </list>
///   Each write (INSERT/UPDATE/DELETE) only maintains 3 B-trees (1 clustered + 2
///   non-clustered), vs 11 in the naive over-indexed table.
/// </summary>
public sealed class OptimizedOverIndexedReviewQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Inserts <paramref name="reviews"/> into <c>ProductReviews</c> (properly-indexed table).
    /// <para>
    /// ✅ Each INSERT maintains only 2 non-clustered indexes + 1 clustered index.<br/>
    /// The application code is identical to the naive version — the speedup is
    /// entirely in the schema design.
    /// </para>
    /// </summary>
    public async Task InsertReviewsAsync(
        IReadOnlyList<ProductReview> reviews,
        CancellationToken cancellationToken = default)
    {
        // ✅ Same AddRange + SaveChanges as naive — only 3 total B-tree updates per row
        context.ProductReviews.AddRange(reviews);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Increments <c>HelpfulVotes</c> for the supplied review IDs.
    /// <para>
    /// ✅ HelpfulVotes is in INCLUDE of IX_Reviews_ProductId_Rating — the UPDATE
    /// still touches that index page, but there are only 2 indexes to maintain
    /// instead of 10, so per-row write cost is dramatically lower.
    /// </para>
    /// </summary>
    public async Task UpdateHelpfulVotesAsync(
        IReadOnlyList<int> reviewIds,
        CancellationToken cancellationToken = default)
    {
        // ✅ Same SQL as naive — difference is schema: far fewer index pages to touch
        await context.ProductReviews
            .Where(r => reviewIds.Contains(r.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.HelpfulVotes, r => r.HelpfulVotes + 1),
                cancellationToken);
    }

    /// <summary>
    /// Top-rated reviews for a product.
    /// ✅ IX_Reviews_ProductId_Rating is a covering index for this exact query:
    ///   (ProductId, Rating DESC) INCLUDE (Title, CreatedAt, IsVerifiedPurchase, HelpfulVotes)
    ///   → index seek, no key-lookups.
    /// </summary>
    public async Task<List<ProductReviewSummary>> GetTopRatedForProductAsync(
        int productId,
        byte minRating = 4,
        CancellationToken cancellationToken = default)
    {
        // ✅ Covering index seek — single unambiguous index chosen by optimizer
        return await context.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Rating >= minRating)
            .OrderByDescending(r => r.Rating)
            .Select(r => new ProductReviewSummary(
                r.Id, r.Title, r.Rating, r.CreatedAt, r.IsVerifiedPurchase, r.HelpfulVotes))
            .ToListAsync(cancellationToken);
    }
}
