using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE order history queries.
///
/// Anti-patterns demonstrated:
///   1. No index on <c>Orders.CustomerId</c> in DbNaive → every customer order
///      lookup performs a full scan of the 1-million-row Orders table.
///   2. Loads the complete <see cref="Order"/> entity including unused columns.
///   3. No <c>AsNoTracking()</c> on read-only queries.
/// </summary>
public sealed class NaiveOrderQueries(NaiveDbContext context)
{
    /// <summary>
    /// Returns all orders for a given customer.
    /// SQL: <c>WHERE CustomerId = @id</c> with no non-clustered index →
    /// SQL Server must scan every row in Orders to find matching records.
    /// </summary>
    public async Task<List<Order>> GetOrdersByCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        // ❌ No index on CustomerId → full table scan on Orders (1M rows)
        // ❌ No AsNoTracking()
        // ❌ Loads full Order entity
        return await context.Orders
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Classic N+1 problem: loads 100 orders then issues one separate query
    /// per order to retrieve its items — 101 round-trips to the database.
    /// </summary>
    public async Task<List<(Order Order, List<OrderItem> Items)>> GetOrdersWithItemsNPlusOneAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        // ❌ Step 1: load N orders — 1 query
        var orders = await context.Orders
            .Take(take)
            .ToListAsync(cancellationToken);

        var result = new List<(Order, List<OrderItem>)>(orders.Count);

        foreach (var order in orders)
        {
            // ❌ Step 2: for EACH order, fire a separate query — N additional queries
            // With take=100 this means 101 total round-trips to the database.
            // On a production system with network latency, this is catastrophic.
            var items = await context.OrderItems
                .Where(i => i.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            result.Add((order, items));
        }

        return result;
    }
}
