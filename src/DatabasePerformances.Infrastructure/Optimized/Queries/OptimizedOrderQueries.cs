using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED order queries.
///
/// Improvements over the naive version:
///   1. <c>IX_Orders_CustomerId</c> in DbOptimized turns the FK lookup from a
///      full table scan into a lightning-fast index seek.
///   2. Single query with <c>AsSplitQuery()</c> replaces the N+1 loop — EF Core
///      issues exactly 2 SQL queries (one for orders, one for all related items)
///      instead of N+1 round-trips.
///   3. INCLUDE columns on the index mean no key-lookup is needed.
/// </summary>
public sealed class OptimizedOrderQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Returns a projection of orders for a customer.
    /// <c>IX_Orders_CustomerId INCLUDE (OrderDate, Status, TotalAmount)</c>
    /// satisfies the entire query without touching the clustered index.
    /// </summary>
    public async Task<List<OrderSummary>> GetOrdersByCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        // ✅ Index seek on IX_Orders_CustomerId (not a scan)
        // ✅ AsNoTracking — read-only query
        // ✅ Projection — only the columns carried in the INCLUDE clause
        return await context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .Select(o => new OrderSummary(
                o.Id,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount,
                o.Items.Count))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Solves the N+1 problem: loads N orders AND their items in exactly
    /// 2 SQL queries using <c>AsSplitQuery()</c>.
    /// <para>
    /// Without AsSplitQuery, EF Core would generate a single JOIN producing a
    /// Cartesian product (order columns repeated for each item). With it, EF Core
    /// issues query 1 for orders and query 2 (<c>WHERE OrderId IN (...)</c>) for
    /// all items at once, then joins them in memory.
    /// </para>
    /// </summary>
    public async Task<List<OrderWithLines>> GetOrdersWithItemsAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        // ✅ AsSplitQuery → 2 queries instead of N+1
        // ✅ AsNoTracking
        // ✅ Projection — no Description (MAX column) loaded
        var orders = await context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .OrderBy(o => o.Id)
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
