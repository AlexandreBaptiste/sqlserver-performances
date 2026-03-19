using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 10 — Tracking vs NoTracking.
/// Verifies that both approaches load the same data (same number of rows,
/// same customer IDs) regardless of change-tracking mode.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class TrackingTests(DatabaseFixture fixture)
{
    private readonly NaiveTrackingQueries _naive       = new(fixture.NaiveContext);
    private readonly OptimizedTrackingQueries _optimized = new(fixture.OptimizedContext);

    [Fact(DisplayName = "Both tracked and untracked queries return the same row count")]
    public async Task SameRowCount()
    {
        const int take = 100;

        var trackedResults   = await _naive.GetCustomersTrackedAsync(take);
        var untrackedResults = await _optimized.GetCustomersNoTrackingAsync(take);

        Assert.Equal(trackedResults.Count, untrackedResults.Count);
    }

    [Fact(DisplayName = "Both tracked and untracked queries return the same customer IDs")]
    public async Task SameCustomerIds()
    {
        const int take = 100;

        var trackedIds   = (await _naive.GetCustomersTrackedAsync(take))
                               .Select(c => c.Id).ToList();
        var untrackedIds = (await _optimized.GetCustomersNoTrackingAsync(take))
                               .Select(c => c.Id).ToList();

        Assert.Equal(trackedIds, untrackedIds);
    }

    [Fact(DisplayName = "Tracked query populates EF change tracker; untracked does not")]
    public async Task TrackingStateVerification()
    {
        // Clear tracker to start clean
        fixture.NaiveContext.ChangeTracker.Clear();
        fixture.OptimizedContext.ChangeTracker.Clear();

        _ = await _naive.GetCustomersTrackedAsync(take: 50);
        var trackedEntries = fixture.NaiveContext.ChangeTracker.Entries().Count();

        // Optimized uses AsNoTracking → context should have 0 tracked entries
        _ = await _optimized.GetCustomersNoTrackingAsync(take: 50);
        var untrackedEntries = fixture.OptimizedContext.ChangeTracker.Entries().Count();

        Assert.True(trackedEntries > 0,
            "Naive query should have tracked entities in the change tracker");
        Assert.Equal(0, untrackedEntries);

        // Cleanup tracker state for other tests
        fixture.NaiveContext.ChangeTracker.Clear();
    }
}
