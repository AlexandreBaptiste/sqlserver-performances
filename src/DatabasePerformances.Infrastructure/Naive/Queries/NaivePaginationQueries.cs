using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE pagination queries using OFFSET / FETCH.
///
/// Anti-patterns demonstrated:
///   1. <c>OFFSET n ROWS FETCH NEXT x ROWS</c> forces SQL Server to generate
///      and then discard <c>n</c> rows before returning the desired page.
///      On page 100 000 this means discarding ~1M rows for every request.
///   2. No covering index on the sort column → SQL Server must sort the whole
///      result set in a worktable before it can apply the offset.
///   3. A separate <c>COUNT(*)</c> query is fired to get the total record count,
///      doubling the number of expensive full-scans per page request.
/// </summary>
public sealed class NaivePaginationQueries(NaiveDbContext context)
{
    /// <summary>
    /// Returns a page of orders using classic SQL OFFSET/FETCH.
    /// Performance degrades linearly with page depth — page 10 000 is 10 000×
    /// more expensive than page 1 because of discarded rows.
    /// </summary>
    public async Task<OrderPage> GetOrderPageAsync(
        int pageNumber,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var offset = (pageNumber - 1) * pageSize;

        // ❌ OFFSET forces SQL Server to generate all rows up to the requested page
        // ❌ No covering index on Id/OrderDate → additional key lookups
        // ❌ Extra COUNT query — scans the table AGAIN just for the total
        var items = await context.Orders
            .AsNoTracking()
            .OrderBy(o => o.Id)
            .Skip(offset)            // → SQL: OFFSET {offset} ROWS
            .Take(pageSize)          // → SQL: FETCH NEXT {pageSize} ROWS ONLY
            .Select(o => new OrderSummary(
                o.Id,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount,
                o.Items.Count))
            .ToListAsync(cancellationToken);

        return new OrderPage(items, null, pageNumber);
    }
}
