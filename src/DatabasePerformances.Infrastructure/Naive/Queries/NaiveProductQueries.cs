using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE product catalog queries.
///
/// Anti-patterns demonstrated:
///   1. No composite index on <c>(CategoryId, Price)</c> → combined filter
///      requires two separate scans or a merge that SQL Server performs poorly.
///   2. <c>ToList()</c> materialises ALL matching products into memory BEFORE
///      applying the price filter — filtering happens in C#, not in SQL.
///   3. Full entity returned instead of a slim projection.
/// </summary>
public sealed class NaiveProductQueries(NaiveDbContext context)
{
    /// <summary>
    /// Returns products filtered by category and price range.
    /// <para>
    /// ❌ In DbNaive there is no composite index on <c>(CategoryId, Price)</c>.
    /// SQL Server must scan the whole Products table, evaluate category, then
    /// evaluate price for each row — two filter steps without index support.
    /// </para>
    /// </summary>
    public async Task<List<Product>> GetByCategoryAndPriceRangeAsync(
        int categoryId,
        decimal minPrice,
        decimal maxPrice,
        CancellationToken cancellationToken = default)
    {
        // ❌ No index on CategoryId or Price → full scan on Products
        // ❌ Loads Description (NVARCHAR(MAX)) even though we never show it here
        // ❌ No AsNoTracking()
        return await context.Products
            .Where(p => p.CategoryId == categoryId
                        && p.Price >= minPrice
                        && p.Price <= maxPrice)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts products per category by materialising ALL products first.
    /// </summary>
    public async Task<Dictionary<int, int>> CountPerCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        // ❌ Loads every Product (including Description) into memory
        // ❌ GroupBy happens in C#, not pushed down to SQL
        var all = await context.Products.ToListAsync(cancellationToken);
        return all.GroupBy(p => p.CategoryId)
                  .ToDictionary(g => g.Key, g => g.Count());
    }
}
