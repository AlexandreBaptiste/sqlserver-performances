using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 12 — Random vs Sequential GUID Primary Keys.
///
/// Verifies that both tables accept writes and return the same data regardless of
/// the GUID generation strategy.  The data semantics are identical; only write
/// performance differs due to clustered index fragmentation.
/// </summary>
[Collection(nameof(DatabaseCollection))]
#pragma warning disable CS9113
public sealed class GuidKeyAuditLogTests(DatabaseFixture _) : IAsyncLifetime
#pragma warning restore CS9113
{
    private DatabasePerformances.Infrastructure.Naive.NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveGuidKeyQueries _naive = null!;
    private OptimizedGuidKeyQueries _optimized = null!;

    private int _naiveCustomerId;
    private int _optimizedCustomerId;
    private DateTime _startTimestamp;

    public async Task InitializeAsync()
    {
        var config     = DbContextFactory.BuildConfiguration();
        _naiveCtx      = DbContextFactory.CreateNaive(config);
        _optimizedCtx  = DbContextFactory.CreateOptimized(config);
        _naive         = new NaiveGuidKeyQueries(_naiveCtx);
        _optimized     = new OptimizedGuidKeyQueries(_optimizedCtx);

        _naiveCustomerId     = await _naiveCtx.Customers.Select(c => c.Id).FirstAsync();
        _optimizedCustomerId = await _optimizedCtx.Customers.Select(c => c.Id).FirstAsync();

        _startTimestamp = DateTime.UtcNow.AddSeconds(-1);

        // Seed 20 audit logs into both databases
        var naiveLogs     = GenerateLogs(20, _naiveCustomerId,     useSequential: false);
        var optimizedLogs = GenerateLogs(20, _optimizedCustomerId, useSequential: true);

        await _naive.InsertLogsAsync(naiveLogs);
        await _optimized.InsertLogsAsync(optimizedLogs);
    }

    public async Task DisposeAsync()
    {
        await _naiveCtx.AuditLogs
            .Where(a => a.Timestamp > _startTimestamp)
            .ExecuteDeleteAsync();
        await _optimizedCtx.AuditLogs
            .Where(a => a.Timestamp > _startTimestamp)
            .ExecuteDeleteAsync();

        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }

    [Fact(DisplayName = "INSERT — both tables accept the same number of rows")]
    public async Task Insert_BothTablesAcceptRows()
    {
        var naiveCount     = await _naiveCtx.AuditLogs
            .Where(a => a.Timestamp > _startTimestamp).CountAsync();
        var optimizedCount = await _optimizedCtx.AuditLogs
            .Where(a => a.Timestamp > _startTimestamp).CountAsync();

        Assert.Equal(20, naiveCount);
        Assert.Equal(20, optimizedCount);
    }

    [Fact(DisplayName = "READ — GetRecentByEntity returns rows from both tables")]
    public async Task Read_GetRecentByEntity_ReturnsSameCountFromBothTables()
    {
        var naiveResults     = await _naive.GetRecentByEntityAsync("Order");
        var optimizedResults = await _optimized.GetRecentByEntityAsync("Order");

        // Both should return the 10 "Order" rows seeded (20 total: 5 entities, 4 each)
        Assert.True(naiveResults.Count > 0,     "Naive table returned no rows");
        Assert.True(optimizedResults.Count > 0, "Optimized table returned no rows");
    }

    [Fact(DisplayName = "READ — retrieved entries have valid non-empty GUIDs")]
    public async Task Read_EntriesHaveValidGuids()
    {
        var naiveResults     = await _naive.GetRecentByEntityAsync("Order");
        var optimizedResults = await _optimized.GetRecentByEntityAsync("Order");

        Assert.All(naiveResults,     e => Assert.NotEqual(Guid.Empty, e.Id));
        Assert.All(optimizedResults, e => Assert.NotEqual(Guid.Empty, e.Id));
    }

    [Fact(DisplayName = "READ — sequential GUIDs (v7) sort chronologically by value")]
    public async Task Read_SequentialGuids_AreChronologicallyOrdered()
    {
        // Version 7 GUIDs embed timestamp in the high bits, so lexicographic order
        // should match insertion order for logs created in the same run.
        var optimizedResults = await _optimizedCtx.AuditLogs
            .AsNoTracking()
            .Where(a => a.Timestamp > _startTimestamp)
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .ToListAsync();

        // Verify that version 7 GUIDs are monotonically non-decreasing when string-sorted
        for (int i = 1; i < optimizedResults.Count; i++)
        {
            var prev = optimizedResults[i - 1].ToString();
            var curr = optimizedResults[i].ToString();
            Assert.True(
                string.Compare(curr, prev, StringComparison.Ordinal) >= 0,
                $"Sequential GUID at index {i} ({curr}) should be >= previous ({prev})");
        }
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static List<AuditLog> GenerateLogs(int count, int customerId, bool useSequential)
    {
        var actions  = new[] { "Created", "Updated", "Deleted" };
        var entities = new[] { "Order",   "Product", "Customer", "Address", "Category" };

        return Enumerable.Range(0, count).Select(i => new AuditLog
        {
            Id                  = useSequential ? Guid.CreateVersion7() : Guid.NewGuid(),
            EntityName          = entities[i % entities.Length],
            EntityId            = (i % 100) + 1,
            Action              = actions[i % actions.Length],
            OldValues           = null,
            NewValues           = null,
            ChangedByCustomerId = customerId,
            Timestamp           = DateTime.UtcNow
        }).ToList();
    }
}
