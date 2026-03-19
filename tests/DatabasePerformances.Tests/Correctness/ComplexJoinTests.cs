using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 7 — Complex Join.
/// Both implementations must return the same orders and item counts.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class ComplexJoinTests(DatabaseFixture fixture)
{
    private readonly NaiveComplexJoinQueries     _naive     = new(fixture.NaiveContext);
    private readonly OptimizedComplexJoinQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Recent orders: both return same count")]
    public async Task GetRecentOrders_SameCount()
    {
        var naiveOrders     = await _naive.GetRecentOrdersWithDetailsAsync(take: 50);
        var optimizedOrders = await _optimized.GetRecentOrdersWithDetailsAsync(take: 50);

        Assert.Equal(naiveOrders.Count, optimizedOrders.Count);
    }

    [Fact(DisplayName = "Recent orders: same order IDs in result set")]
    public async Task GetRecentOrders_SameOrderIds()
    {
        var naiveIds     = (await _naive.GetRecentOrdersWithDetailsAsync(take: 50))
                               .Select(r => r.Order.Id).OrderBy(id => id).ToList();
        var optimizedIds = (await _optimized.GetRecentOrdersWithDetailsAsync(take: 50))
                               .Select(r => r.Order.Id).OrderBy(id => id).ToList();

        Assert.Equal(naiveIds, optimizedIds);
    }

    [Fact(DisplayName = "Recent orders: line item counts match for each order")]
    public async Task GetRecentOrders_SameLineItemCounts()
    {
        var naiveCounts     = (await _naive.GetRecentOrdersWithDetailsAsync(take: 20))
                                  .ToDictionary(r => r.Order.Id, r => r.Lines.Count);
        var optimizedCounts = (await _optimized.GetRecentOrdersWithDetailsAsync(take: 20))
                                  .ToDictionary(r => r.Order.Id, r => r.Lines.Count);

        foreach (var (orderId, naiveCount) in naiveCounts)
        {
            Assert.Equal(naiveCount, optimizedCounts[orderId]);
        }
    }
}
