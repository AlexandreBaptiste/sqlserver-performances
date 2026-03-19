using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED pagination using keyset (cursor-based) pagination.
///
/// Improvements over the naive OFFSET/FETCH approach:
///   1. Keyset pagination uses <c>WHERE Id &gt; @lastId ORDER BY Id</c> — SQL
///      Server performs an index seek directly to the cursor position.
///      Cost is <b>O(1)</b> regardless of page depth, versus O(n) for OFFSET.
///   2. <c>IX_Orders_Status_Id INCLUDE (...)</c> satisfies the WHERE + ORDER
///      + SELECT in a single index scan with no key-lookup.
///   3. No secondary COUNT(*) query — the cursor approach does not need total rows.
/// </summary>
public sealed class OptimizedPaginationQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Returns the next page of orders after the supplied cursor.
    /// Pass <c>lastSeenId = null</c> for the first page.
    /// The response includes <c>NextCursorId</c> — store it client-side and
    /// pass it as <paramref name="lastSeenId"/> to fetch the next page.
    /// </summary>
    public async Task<OrderPage> GetOrderPageAsync(
        int? lastSeenId = null,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // ✅ Keyset predicate → index seek, O(1) regardless of depth
        // ✅ IX_Orders_Status_Id covering index — no heap access
        // ✅ AsNoTracking + projection
        var baseQuery = context.Orders.AsNoTracking();

        if (lastSeenId is not null)
        {
            // Seek past the last seen record — single index seek, no rows discarded
            baseQuery = baseQuery.Where(o => o.Id > lastSeenId.Value);
        }

        var items = await baseQuery
            .OrderBy(o => o.Id)
            .Take(pageSize)
            .Select(o => new OrderSummary(
                o.Id,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount,
                o.Items.Count))
            .ToListAsync(cancellationToken);

        var nextCursor = items.Count == pageSize ? items[^1].Id : (int?)null;
        return new OrderPage(items, nextCursor, 0 /* page number N/A for keyset */);
    }
}
