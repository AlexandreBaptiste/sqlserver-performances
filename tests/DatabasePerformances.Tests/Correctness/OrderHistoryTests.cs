using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 2 — Order History and N+1.
/// Both implementations must return the same order IDs for a given customer,
/// and the N+1 vs. AsSplitQuery approaches must produce matching data.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class OrderHistoryTests(DatabaseFixture fixture)
{
    private readonly NaiveOrderQueries     _naive     = new(fixture.NaiveContext);
    private readonly OptimizedOrderQueries _optimized = new(fixture.OptimizedContext);

    private const int TestCustomerId = 1;

    [Fact(DisplayName = "Order history: both return same order IDs for a customer")]
    public async Task GetOrdersByCustomer_SameOrderIds()
    {
        var naiveOrders     = await _naive.GetOrdersByCustomerAsync(TestCustomerId);
        var optimizedOrders = await _optimized.GetOrdersByCustomerAsync(TestCustomerId);

        var naiveIds     = naiveOrders.Select(o => o.Id).OrderBy(id => id).ToList();
        var optimizedIds = optimizedOrders.Select(o => o.Id).OrderBy(id => id).ToList();

        Assert.Equal(naiveIds, optimizedIds);
    }

    [Fact(DisplayName = "N+1 vs AsSplitQuery: same order IDs in first 100")]
    public async Task GetOrdersWithItems_SameOrderIds()
    {
        var naiveResult     = await _naive.GetOrdersWithItemsNPlusOneAsync(take: 100);
        var optimizedResult = await _optimized.GetOrdersWithItemsAsync(take: 100);

        var naiveOrderIds     = naiveResult.Select(t => t.Order.Id).OrderBy(id => id).ToList();
        var optimizedOrderIds = optimizedResult.Select(r => r.Order.Id).OrderBy(id => id).ToList();

        Assert.Equal(naiveOrderIds, optimizedOrderIds);
    }

    [Fact(DisplayName = "N+1 vs AsSplitQuery: item counts match for same orders")]
    public async Task GetOrdersWithItems_SameItemCounts()
    {
        var naiveResult     = await _naive.GetOrdersWithItemsNPlusOneAsync(take: 50);
        var optimizedResult = await _optimized.GetOrdersWithItemsAsync(take: 50);

        var naiveItemCounts     = naiveResult.ToDictionary(t => t.Order.Id, t => t.Items.Count);
        var optimizedItemCounts = optimizedResult.ToDictionary(r => r.Order.Id, r => r.Lines.Count);

        foreach (var (orderId, naiveCount) in naiveItemCounts)
        {
            Assert.Equal(naiveCount, optimizedItemCounts[orderId]);
        }
    }
}
