using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 6 — Pagination.
/// OFFSET/FETCH (naive) and keyset (optimized) must return the same records
/// when positioned at the same logical location.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class PaginationTests(DatabaseFixture fixture)
{
    private readonly NaivePaginationQueries     _naive     = new(fixture.NaiveContext);
    private readonly OptimizedPaginationQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "First page: both return 20 items")]
    public async Task FirstPage_ReturnsTwentyItems()
    {
        var naivePage     = await _naive.GetOrderPageAsync(pageNumber: 1, pageSize: 20);
        var optimizedPage = await _optimized.GetOrderPageAsync(lastSeenId: null, pageSize: 20);

        Assert.Equal(20, naivePage.Items.Count);
        Assert.Equal(20, optimizedPage.Items.Count);
    }

    [Fact(DisplayName = "First page: same order IDs in both results")]
    public async Task FirstPage_SameOrderIds()
    {
        var naivePage     = await _naive.GetOrderPageAsync(pageNumber: 1, pageSize: 20);
        var optimizedPage = await _optimized.GetOrderPageAsync(lastSeenId: null, pageSize: 20);

        var naiveIds     = naivePage.Items.Select(o => o.Id).ToList();
        var optimizedIds = optimizedPage.Items.Select(o => o.Id).ToList();

        Assert.Equal(naiveIds, optimizedIds);
    }

    [Fact(DisplayName = "Optimized: NextCursorId is set when page is full")]
    public async Task KeysetPage_NextCursorIdIsSet()
    {
        var page = await _optimized.GetOrderPageAsync(lastSeenId: null, pageSize: 20);
        Assert.NotNull(page.NextCursorId);
    }

    [Fact(DisplayName = "Optimized: consecutive pages have no overlap")]
    public async Task KeysetPagination_NoDuplicatesBetweenPages()
    {
        var page1 = await _optimized.GetOrderPageAsync(lastSeenId: null, pageSize: 20);
        var page2 = await _optimized.GetOrderPageAsync(lastSeenId: page1.NextCursorId, pageSize: 20);

        var ids1 = page1.Items.Select(o => o.Id).ToHashSet();
        var ids2 = page2.Items.Select(o => o.Id).ToHashSet();

        Assert.Empty(ids1.Intersect(ids2));
    }
}
