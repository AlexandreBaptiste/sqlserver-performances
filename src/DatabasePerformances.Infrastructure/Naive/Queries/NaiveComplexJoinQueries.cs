using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE complex join query — order summary with customer and line details.
///
/// Anti-patterns demonstrated:
///   1. <c>.Include()</c> without <c>.AsSplitQuery()</c> on a 1-to-many
///      relationship creates a Cartesian product: if an order has 5 items,
///      all customer columns are duplicated 5 times in the result set.
///   2. No <c>AsNoTracking()</c> — EF Core tracks hundreds of nested entities.
///   3. Loading <c>Description</c> (NVARCHAR(MAX)) from Products even though
///      the summary view never displays it.
/// </summary>
public sealed class NaiveComplexJoinQueries(NaiveDbContext context)
{
    /// <summary>
    /// Loads recent orders including customer info and all line items.
    /// ❌ Without <c>AsSplitQuery()</c> and with no FK indexes this produces a
    /// huge Cartesian-product result set that EF Core then stitches together in memory.
    /// </summary>
    public async Task<List<OrderWithLines>> GetRecentOrdersWithDetailsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        // ❌ No AsSplitQuery() → Cartesian product in SELECT result
        // ❌ Includes Product.Description (NVARCHAR(MAX)) — wasteful for a list view
        // ❌ No covering index on Orders.OrderDate → sort + scan
        var orders = await context.Orders
            .Include(o => o.Customer)
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
