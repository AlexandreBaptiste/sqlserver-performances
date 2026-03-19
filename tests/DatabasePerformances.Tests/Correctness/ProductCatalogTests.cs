using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 3 — Product Catalog Filtering.
/// Naive and optimized must return the same product IDs for identical filters.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class ProductCatalogTests(DatabaseFixture fixture)
{
    private readonly NaiveProductQueries     _naive     = new(fixture.NaiveContext);
    private readonly OptimizedProductQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Product filter: same product IDs returned")]
    public async Task GetByCategoryAndPrice_SameProductIds()
    {
        var naiveProducts     = await _naive.GetByCategoryAndPriceRangeAsync(1, 10, 500);
        var optimizedProducts = await _optimized.GetByCategoryAndPriceRangeAsync(1, 10, 500);

        var naiveIds     = naiveProducts.Select(p => p.Id).OrderBy(id => id).ToList();
        var optimizedIds = optimizedProducts.Select(p => p.Id).OrderBy(id => id).ToList();

        Assert.Equal(naiveIds, optimizedIds);
    }

    [Fact(DisplayName = "Product filter: all results satisfy price range constraint")]
    public async Task GetByCategoryAndPrice_AllResultsInRange()
    {
        const decimal min = 10m;
        const decimal max = 500m;

        var optimizedProducts = await _optimized.GetByCategoryAndPriceRangeAsync(1, min, max);

        Assert.All(optimizedProducts, p =>
        {
            Assert.True(p.Price >= min, $"Product {p.Id} price {p.Price} is below min {min}");
            Assert.True(p.Price <= max, $"Product {p.Id} price {p.Price} is above max {max}");
        });
    }

    [Fact(DisplayName = "Category count: both return same category totals")]
    public async Task CountPerCategory_SameTotals()
    {
        var naiveCounts     = await _naive.CountPerCategoryAsync();
        var optimizedCounts = await _optimized.CountPerCategoryAsync();

        Assert.Equal(naiveCounts.OrderBy(kv => kv.Key).ToList(),
                     optimizedCounts.OrderBy(kv => kv.Key).ToList());
    }
}
