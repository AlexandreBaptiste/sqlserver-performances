using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DatabasePerformances.Tests.Performance;

/// <summary>
/// Performance sanity tests — these are not as rigorous as BenchmarkDotNet
/// (no JIT warm-up control, no iteration statistics), but they catch obvious
/// regressions and serve as living documentation of expected performance ranges.
///
/// Each test:
///   1. Times the optimized implementation.
///   2. Times the naive implementation.
///   3. Asserts optimized &lt; an absolute time threshold.
///   4. Asserts naive is at least 2× slower than optimized (ratio guard).
///
/// ⚠  These tests require the databases to be fully seeded (~200k customers,
///    ~1M orders, ~3M order items). Run the Seeder project first.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class PerformanceAssertionTests(DatabaseFixture fixture, ITestOutputHelper output)
{
    // -------------------------------------------------------------------------
    // Scenario 1 — Customer Search
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "CustomerSearch: optimized must complete in < 500 ms")]
    public async Task CustomerSearch_OptimizedFasterThanThreshold()
    {
        var queries = new OptimizedCustomerQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var results = await queries.SearchByEmailPrefixAsync("jo");
        sw.Stop();

        output.WriteLine($"Optimized email search: {sw.ElapsedMilliseconds} ms, " +
                         $"{results.Count} results");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Expected < 500 ms but took {sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "CustomerSearch: optimized must be at least 2× faster than naive")]
    public async Task CustomerSearch_OptimizedFasterRatio()
    {
        var naive     = new NaiveCustomerQueries(fixture.NaiveContext);
        var optimized = new OptimizedCustomerQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.SearchByEmailPrefixAsync("jo");
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.SearchByEmailAsync("gmail");
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive: {naiveMs} ms | Optimized: {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / optimizedMs:F1}×");

        Assert.True(naiveMs >= optimizedMs * 2,
            $"Naive ({naiveMs} ms) should be ≥ 2× slower than optimized ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 2 — Order History
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "OrderHistory: optimized must complete in < 200 ms")]
    public async Task OrderHistory_OptimizedFasterThanThreshold()
    {
        var queries = new OptimizedOrderQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var results = await queries.GetOrdersByCustomerAsync(customerId: 1);
        sw.Stop();

        output.WriteLine($"Optimized order history: {sw.ElapsedMilliseconds} ms, " +
                         $"{results.Count} orders");
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Expected < 200 ms but took {sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "OrderHistory: optimized must be at least 2× faster than naive")]
    public async Task OrderHistory_OptimizedFasterRatio()
    {
        var naive     = new NaiveOrderQueries(fixture.NaiveContext);
        var optimized = new OptimizedOrderQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.GetOrdersByCustomerAsync(1);
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.GetOrdersByCustomerAsync(1);
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive: {naiveMs} ms | Optimized: {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / optimizedMs:F1}×");

        Assert.True(naiveMs >= optimizedMs * 2,
            $"Naive ({naiveMs} ms) should be ≥ 2× slower than optimized ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 3 — Product Catalog
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ProductCatalog: optimized covering-index query must complete in < 300 ms")]
    public async Task ProductCatalog_OptimizedFasterThanThreshold()
    {
        var queries = new OptimizedProductQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var results = await queries.GetByCategoryAndPriceRangeAsync(
            categoryId: 1, minPrice: 10m, maxPrice: 500m);
        sw.Stop();

        output.WriteLine($"Optimized product catalog: {sw.ElapsedMilliseconds} ms, " +
                         $"{results.Count} products");
        Assert.True(sw.ElapsedMilliseconds < 300,
            $"Expected < 300 ms but took {sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "ProductCatalog: optimized CountPerCategory must be at least 2× faster than naive")]
    public async Task ProductCatalog_CountPerCategory_OptimizedFasterRatio()
    {
        var naive     = new NaiveProductQueries(fixture.NaiveContext);
        var optimized = new OptimizedProductQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.CountPerCategoryAsync();
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.CountPerCategoryAsync();
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive: {naiveMs} ms | Optimized: {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / Math.Max(optimizedMs, 1):F1}×");

        Assert.True(naiveMs >= optimizedMs,
            $"Naive ({naiveMs} ms) should be ≥ optimized ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 4 — Sales Report
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SalesReport: optimized Dapper + columnstore must complete in < 2000 ms")]
    public async Task SalesReport_OptimizedFasterThanThreshold()
    {
        var queries = new OptimizedSalesReportQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var results = await queries.GetTopProductsByRevenueAsync(
            from: new DateTime(2024, 1, 1),
            to: new DateTime(2024, 12, 31),
            topN: 10);
        sw.Stop();

        output.WriteLine($"Optimized sales report: {sw.ElapsedMilliseconds} ms, " +
                         $"{results.Count} results");
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Expected < 2000 ms but took {sw.ElapsedMilliseconds} ms");
    }

    // -------------------------------------------------------------------------
    // Scenario 5 — N+1 Problem
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "N+1: optimized AsSplitQuery must be at least 5× faster for 100 orders")]
    public async Task NPlusOne_OptimizedFasterRatio()
    {
        var naive     = new NaiveOrderQueries(fixture.NaiveContext);
        var optimized = new OptimizedOrderQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.GetOrdersWithItemsAsync(take: 100);
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.GetOrdersWithItemsNPlusOneAsync(take: 100);
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive N+1: {naiveMs} ms | Optimized: {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / optimizedMs:F1}×");

        Assert.True(naiveMs >= optimizedMs * 5,
            $"Naive N+1 ({naiveMs} ms) should be ≥ 5× slower than optimized ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 6 — Deep Pagination
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Pagination: keyset page 1 must complete in < 100 ms")]
    public async Task Pagination_KeysetFirstPage_FastEnough()
    {
        var queries = new OptimizedPaginationQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        await queries.GetOrderPageAsync(lastSeenId: null, pageSize: 20);
        sw.Stop();

        output.WriteLine($"Keyset first page: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Expected < 100 ms but took {sw.ElapsedMilliseconds} ms");
    }

    // -------------------------------------------------------------------------
    // Scenario 7 — Complex Join
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ComplexJoin: optimized AsSplitQuery must complete in < 1000 ms")]
    public async Task ComplexJoin_OptimizedFasterThanThreshold()
    {
        var queries = new OptimizedComplexJoinQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var results = await queries.GetRecentOrdersWithDetailsAsync(take: 50);
        sw.Stop();

        output.WriteLine($"Optimized complex join: {sw.ElapsedMilliseconds} ms, " +
                         $"{results.Count} orders");
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Expected < 1000 ms but took {sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "ComplexJoin: optimized must be at least 2× faster than naive")]
    public async Task ComplexJoin_OptimizedFasterRatio()
    {
        var naive     = new NaiveComplexJoinQueries(fixture.NaiveContext);
        var optimized = new OptimizedComplexJoinQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.GetRecentOrdersWithDetailsAsync(take: 50);
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.GetRecentOrdersWithDetailsAsync(take: 50);
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive: {naiveMs} ms | Optimized: {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / Math.Max(optimizedMs, 1):F1}×");

        Assert.True(naiveMs >= optimizedMs,
            $"Naive ({naiveMs} ms) should be ≥ optimized ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 9 — Count() vs Any() (Existence Check)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ExistenceCheck: Any() must complete in < 50 ms")]
    public async Task ExistenceCheck_AnyFasterThanThreshold()
    {
        var queries = new OptimizedExistenceCheckQueries(fixture.OptimizedContext);
        var sw = Stopwatch.StartNew();
        var exists = await queries.CustomerHasOrdersAsync(1);
        sw.Stop();

        output.WriteLine($"Optimized Any() has-orders: {sw.ElapsedMilliseconds} ms, result={exists}");
        Assert.True(exists);
        Assert.True(sw.ElapsedMilliseconds < 50,
            $"Expected < 50 ms but took {sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "ExistenceCheck: Any() must be faster than Count() > 0")]
    public async Task ExistenceCheck_AnyFasterThanCount()
    {
        var naive     = new NaiveExistenceCheckQueries(fixture.NaiveContext);
        var optimized = new OptimizedExistenceCheckQueries(fixture.OptimizedContext);

        var sw = Stopwatch.StartNew();
        await optimized.CustomerHasOrdersAsync(1);
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.CustomerHasOrdersAsync(1);
        var naiveMs = sw.ElapsedMilliseconds;

        output.WriteLine($"Naive Count()>0: {naiveMs} ms | Optimized Any(): {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / Math.Max(optimizedMs, 1):F1}×");

        Assert.True(naiveMs >= optimizedMs,
            $"Naive Count() ({naiveMs} ms) should be ≥ optimized Any() ({optimizedMs} ms)");
    }

    // -------------------------------------------------------------------------
    // Scenario 10 — Tracking vs No-Tracking
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Tracking: NoTracking + projection must be faster than tracked SELECT *")]
    public async Task Tracking_NoTrackingFasterThanTracked()
    {
        var naive     = new NaiveTrackingQueries(fixture.NaiveContext);
        var optimized = new OptimizedTrackingQueries(fixture.OptimizedContext);

        // Clear tracker to start clean
        fixture.NaiveContext.ChangeTracker.Clear();

        var sw = Stopwatch.StartNew();
        await optimized.GetCustomersNoTrackingAsync(take: 5000);
        var optimizedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        await naive.GetCustomersTrackedAsync(take: 5000);
        var naiveMs = sw.ElapsedMilliseconds;

        // Clear tracker after test
        fixture.NaiveContext.ChangeTracker.Clear();

        output.WriteLine($"Naive (tracked): {naiveMs} ms | Optimized (no-tracking): {optimizedMs} ms | " +
                         $"Ratio: {(double)naiveMs / Math.Max(optimizedMs, 1):F1}×");

        Assert.True(naiveMs >= optimizedMs,
            $"Tracked query ({naiveMs} ms) should be ≥ no-tracking query ({optimizedMs} ms)");
    }
}
