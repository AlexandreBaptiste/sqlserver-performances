using Dapper;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED sales report queries.
///
/// Improvements over the naive version:
///   1. The non-clustered <b>columnstore index</b> on <c>OrderItems</c> allows
///      SQL Server to switch to batch-mode execution — processing 900+ rows per
///      CPU cycle instead of one row at a time (row-mode). Typical speedup for
///      GROUP BY / SUM over millions of rows: 5× – 100×.
///   2. Dapper is used for the hot-path report query because it bypasses EF
///      Core's materialisation overhead — ideal for read-only aggregations.
///   3. The date filter uses <c>IX_Orders_OrderDate</c>, immediately pruning
///      the join before the columnstore aggregation begins.
/// </summary>
public sealed class OptimizedSalesReportQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Top <paramref name="topN"/> products by revenue over a date window.
    /// Uses Dapper + raw SQL to leverage the columnstore index with batch-mode
    /// execution for maximum throughput on large aggregations.
    /// </summary>
    public async Task<List<ProductRevenueItem>> GetTopProductsByRevenueAsync(
        DateTime from,
        DateTime to,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        // ✅ Raw SQL via Dapper — EF Core overhead avoided for read-only aggregation
        // ✅ SQL Server uses IX_OrderItems_Columnstore → batch-mode GROUP BY
        // ✅ IX_Orders_OrderDate prunes the date range before the join
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string is null.");

        const string sql = """
            SELECT
                p.Id            AS ProductId,
                p.Name          AS ProductName,
                c.Name          AS CategoryName,
                SUM(CAST(oi.Quantity AS DECIMAL(18,2)) * oi.UnitPrice) AS TotalRevenue,
                SUM(oi.Quantity)                                        AS TotalQuantitySold
            FROM   dbo.Orders      o
            JOIN   dbo.OrderItems  oi ON oi.OrderId   = o.Id
            JOIN   dbo.Products    p  ON p.Id          = oi.ProductId
            JOIN   dbo.Categories  c  ON c.Id          = p.CategoryId
            WHERE  o.OrderDate BETWEEN @From AND @To
            GROUP  BY p.Id, p.Name, c.Name
            ORDER  BY TotalRevenue DESC
            OFFSET 0 ROWS FETCH NEXT @TopN ROWS ONLY;
            """;

        await using var conn = new SqlConnection(connectionString);
        var cmd = new CommandDefinition(
            sql,
            new { From = from, To = to, TopN = topN },
            cancellationToken: cancellationToken);

        var rows = await conn.QueryAsync<ProductRevenueItem>(cmd);
        return rows.ToList();
    }

    /// <summary>
    /// Monthly revenue roll-up by category using the columnstore index.
    /// </summary>
    public async Task<List<MonthlySalesItem>> GetMonthlySalesByCategoryAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string is null.");

        // ✅ Columnstore index enables batch-mode execution on the aggregation
        // ✅ Sargable date range filter — allows IX_Orders_OrderDate seek
        //    (using YEAR(col) = @x would wrap the column in a function, preventing seek)
        const string sql = """
            SELECT
                YEAR(o.OrderDate)   AS Year,
                MONTH(o.OrderDate)  AS Month,
                c.Name              AS CategoryName,
                SUM(CAST(oi.Quantity AS DECIMAL(18,2)) * oi.UnitPrice) AS TotalRevenue,
                COUNT(DISTINCT o.Id)                                    AS OrderCount
            FROM   dbo.Orders     o
            JOIN   dbo.OrderItems oi ON oi.OrderId  = o.Id
            JOIN   dbo.Products   p  ON p.Id         = oi.ProductId
            JOIN   dbo.Categories c  ON c.Id         = p.CategoryId
            WHERE  o.OrderDate >= @YearStart AND o.OrderDate < @YearEnd
            GROUP  BY YEAR(o.OrderDate), MONTH(o.OrderDate), c.Name
            ORDER  BY Year, Month;
            """;

        await using var conn = new SqlConnection(connectionString);
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd   = new DateTime(year + 1, 1, 1);
        var cmd = new CommandDefinition(sql, new { YearStart = yearStart, YearEnd = yearEnd }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<MonthlySalesItem>(cmd);
        return rows.ToList();
    }
}
