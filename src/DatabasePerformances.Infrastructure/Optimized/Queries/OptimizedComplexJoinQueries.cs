using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED complex join — order summary with customer and line details.
///
/// Improvements over the naive version:
///   1. <c>AsSplitQuery()</c> prevents the Cartesian-product result set.
///      EF Core issues 3 focused queries instead of one massive JOIN.
///   2. Explicit projections avoid loading <c>Description</c> (NVARCHAR(MAX)).
///   3. FK indexes (<c>IX_Orders_OrderDate</c>, <c>IX_OrderItems_OrderId</c>)
///      in DbOptimized turn each join from a scan into a seek.
///   4. <c>AsNoTracking()</c> — no change-tracking for a read-only summary.
/// </summary>
public sealed class OptimizedComplexJoinQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Loads recent orders with customer info and line items efficiently.
    /// </summary>
    public async Task<List<OrderWithLines>> GetRecentOrdersWithDetailsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        // ✅ AsSplitQuery → 3 targeted queries, no Cartesian product
        // ✅ AsNoTracking — read-only
        // ✅ IX_Orders_OrderDate seek for ORDER BY + TAKE
        // ✅ IX_OrderItems_OrderId seek when EF Core fetches the items batch
        var orders = await context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Category)
            .OrderByDescending(o => o.OrderDate)
            .Take(take)
            .ToListAsync(cancellationToken);

        return orders.Select(o => new OrderWithLines(
            new OrderSummary(o.Id, o.OrderDate, o.Status.ToString(), o.TotalAmount, o.Items.Count),
            o.Items.Select(i => new OrderLineDetail(
                i.Id, i.ProductId, i.Product.Name, i.Quantity, i.UnitPrice))
                .ToList()
                .AsReadOnly()
        )).ToList();
    }
}
