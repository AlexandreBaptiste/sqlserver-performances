using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenarios 4 and 7 — Sales Reports.
/// Both implementations must return the same top products and category roll-ups.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class SalesReportTests(DatabaseFixture fixture)
{
    private readonly NaiveSalesReportQueries     _naive     = new(fixture.NaiveContext);
    private readonly OptimizedSalesReportQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Top products: both return same number of results")]
    public async Task GetTopProducts_SameCount()
    {
        var from = DateTime.UtcNow.AddDays(-180);
        var to   = DateTime.UtcNow;

        var naiveResults     = await _naive.GetTopProductsByRevenueAsync(from, to, topN: 10);
        var optimizedResults = await _optimized.GetTopProductsByRevenueAsync(from, to, topN: 10);

        Assert.Equal(naiveResults.Count, optimizedResults.Count);
    }

    [Fact(DisplayName = "Top products: same product IDs in top 10")]
    public async Task GetTopProducts_SameProductIds()
    {
        var from = DateTime.UtcNow.AddDays(-365);
        var to   = DateTime.UtcNow;

        var naiveIds     = (await _naive.GetTopProductsByRevenueAsync(from, to, 10))
                               .Select(r => r.ProductId).OrderBy(id => id).ToList();
        var optimizedIds = (await _optimized.GetTopProductsByRevenueAsync(from, to, 10))
                               .Select(r => r.ProductId).OrderBy(id => id).ToList();

        Assert.Equal(naiveIds, optimizedIds);
    }

    [Fact(DisplayName = "Monthly sales: both produce same number of months")]
    public async Task GetMonthlySales_SameMonthCount()
    {
        var naiveMonths     = await _naive.GetMonthlySalesByCategoryAsync(DateTime.UtcNow.Year - 1);
        var optimizedMonths = await _optimized.GetMonthlySalesByCategoryAsync(DateTime.UtcNow.Year - 1);

        Assert.Equal(naiveMonths.Count, optimizedMonths.Count);
    }
}
