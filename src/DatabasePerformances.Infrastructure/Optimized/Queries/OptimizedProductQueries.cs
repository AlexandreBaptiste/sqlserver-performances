using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED product catalog queries.
///
/// Improvements over the naive version:
///   1. Composite index <c>IX_Products_CategoryId_Price INCLUDE (Name, Stock)</c>
///      allows SQL Server to satisfy the entire WHERE + SELECT from the index
///      pages alone — no touch of the clustered index row (covering index).
///   2. GROUP BY is pushed down to SQL, not done in C#.
///   3. <c>AsNoTracking()</c> + projection — only needed columns.
/// </summary>
public sealed class OptimizedProductQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Returns products filtered by category and price range.
    /// The composite index <c>(CategoryId, Price) INCLUDE (Name, Stock)</c>
    /// satisfies both the WHERE clause and the SELECT projection without
    /// accessing the clustered index — a pure covering-index scan.
    /// </summary>
    public async Task<List<ProductCatalogItem>> GetByCategoryAndPriceRangeAsync(
        int categoryId,
        decimal minPrice,
        decimal maxPrice,
        CancellationToken cancellationToken = default)
    {
        // ✅ Index seek on IX_Products_CategoryId_Price
        // ✅ Covering index — Name and Stock in INCLUDE, no key-lookup needed
        // ✅ AsNoTracking + minimal projection
        return await context.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId
                        && p.Price >= minPrice
                        && p.Price <= maxPrice)
            .Select(p => new ProductCatalogItem(
                p.Id,
                p.Name,
                p.Category.Name,
                p.Price,
                p.Stock))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts products per category — GROUP BY pushed down to SQL.
    /// </summary>
    public async Task<Dictionary<int, int>> CountPerCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        // ✅ GroupBy executed in SQL, not in C# memory
        // ✅ Only aggregated scalars returned (no entity allocation)
        return await context.Products
            .AsNoTracking()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);
    }
}
