using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE sales report queries.
///
/// Anti-patterns demonstrated:
///   1. No columnstore index → SQL Server uses row-store execution to aggregate
///      millions of OrderItems rows one page at a time.
///   2. LINQ joins are translated to nested-loop or hash joins without statistical
///      guidance because DbNaive has no updated statistics or covering indexes.
///   3. All columns of all joined tables are loaded into memory.
/// </summary>
public sealed class NaiveSalesReportQueries(NaiveDbContext context)
{
    /// <summary>
    /// Returns the top <paramref name="topN"/> products by revenue over the
    /// supplied date window, joining Orders → OrderItems → Products → Categories.
    /// <para>
    /// ❌ No index on <c>Orders.OrderDate</c> → the date filter scans all 1M orders.<br/>
    /// ❌ No index on <c>OrderItems.OrderId</c> → every item lookup is a scan.<br/>
    /// ❌ No columnstore index → aggregation processes row-by-row in row store mode.
    /// </para>
    /// </summary>
    public async Task<List<ProductRevenueItem>> GetTopProductsByRevenueAsync(
        DateTime from,
        DateTime to,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        // ❌ Full scan on Orders (no index on OrderDate)
        // ❌ Nested-loop join on OrderItems.OrderId (no index)
        // ❌ Aggregation forced to row-store mode without columnstore
        return await context.Orders
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, ProductName = i.Product.Name, CategoryName = i.Product.Category.Name })
            .Select(g => new ProductRevenueItem(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.CategoryName,
                g.Sum(i => i.Quantity * i.UnitPrice),
                g.Sum(i => i.Quantity)))
            .OrderByDescending(r => r.TotalRevenue)
            .Take(topN)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Monthly revenue roll-up by category.
    /// ❌ Aggregates all OrderItems rows without columnstore benefit.
    /// </summary>
    public async Task<List<MonthlySalesItem>> GetMonthlySalesByCategoryAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        // ❌ Scans entire OrderItems table (3M rows) with no columnstore
        // ❌ Two nested joins (Order → OrderItem → Product→ Category) without covering indexes
        return await context.OrderItems
            .Where(i => i.Order.OrderDate.Year == year)
            .GroupBy(i => new
            {
                i.Order.OrderDate.Year,
                i.Order.OrderDate.Month,
                CategoryName = i.Product.Category.Name
            })
            .Select(g => new MonthlySalesItem(
                g.Key.Year,
                g.Key.Month,
                g.Key.CategoryName,
                g.Sum(i => i.Quantity * i.UnitPrice),
                g.Select(i => i.OrderId).Distinct().Count()))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync(cancellationToken);
    }
}
