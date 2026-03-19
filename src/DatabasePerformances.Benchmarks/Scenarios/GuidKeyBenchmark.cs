using BenchmarkDotNet.Attributes;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 12 — Random vs Sequential GUID Primary Keys
/// =====================================================================
/// Compares INSERT performance on two identical <c>AuditLogs</c> tables
/// whose only difference is how the GUID primary key is generated:
///
/// Naive:     <c>Guid.NewGuid()</c>        → random  → ~50% page-split rate → fragmentation → slow
/// Optimized: <c>Guid.CreateVersion7()</c> → ordered → no page splits       → sequential I/O → fast
///
/// Both <c>AuditLogs</c> tables have an identical schema (same UNIQUEIDENTIFIER PK,
/// same NONCLUSTERED index on Timestamp).  The performance difference comes entirely
/// from which value is stored in the clustered key column.
///
/// Key insight: Using a GUID as a clustered primary key is a classic SQL Server anti-pattern
/// when the GUID is randomly generated.  The fix is a one-word change in application code:
/// <c>Guid.CreateVersion7()</c> instead of <c>Guid.NewGuid()</c>.
///
/// Expected ratio: 1.5×–3× slower inserts with random GUIDs on a warm table.
/// The gap widens as the table grows because fragmentation accumulates over time.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("GuidKey")]
public class GuidKeyBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveGuidKeyQueries _naive = null!;
    private OptimizedGuidKeyQueries _optimized = null!;

    private int _naiveCustomerId;
    private int _optimizedCustomerId;

    // Max Ids before benchmark inserts (used for safe per-iteration cleanup)
    // AuditLogs uses GUID PK so we track by Timestamp range instead
    private DateTime _setupTimestamp;

    private List<AuditLog> _naiveInsertBatch = null!;
    private List<AuditLog> _optimizedInsertBatch = null!;

    private const int BatchSize = 1_000;

    [GlobalSetup]
    public async Task Setup()
    {
        var config     = DbContextFactory.BuildConfiguration();
        _naiveCtx      = DbContextFactory.CreateNaive(config);
        _optimizedCtx  = DbContextFactory.CreateOptimized(config);
        _naive         = new NaiveGuidKeyQueries(_naiveCtx);
        _optimized     = new OptimizedGuidKeyQueries(_optimizedCtx);

        _naiveCustomerId     = await _naiveCtx.Customers.Select(c => c.Id).FirstAsync();
        _optimizedCustomerId = await _optimizedCtx.Customers.Select(c => c.Id).FirstAsync();

        // Record the time just before benchmarking so cleanup only removes our rows
        _setupTimestamp = DateTime.UtcNow.AddSeconds(-1);

        // Pre-warm both tables with 5 000 rows so fragmentation differences become
        // observable (empty tables rarely split pages)
        var naiveWarm      = GenerateLogs(5_000, _naiveCustomerId,     useSequential: false);
        var optimizedWarm  = GenerateLogs(5_000, _optimizedCustomerId, useSequential: true);
        await _naive.InsertLogsAsync(naiveWarm);
        await _optimized.InsertLogsAsync(optimizedWarm);
    }

    [IterationSetup]
    public void GenerateBatches()
    {
        // Fresh GUID-keyed entities each iteration
        _naiveInsertBatch     = GenerateLogs(BatchSize, _naiveCustomerId,     useSequential: false);
        _optimizedInsertBatch = GenerateLogs(BatchSize, _optimizedCustomerId, useSequential: true);
    }

    [IterationCleanup]
    public async Task DeleteInsertedRows()
    {
        await _naiveCtx.AuditLogs
            .Where(a => a.Timestamp > _setupTimestamp)
            .ExecuteDeleteAsync();

        await _optimizedCtx.AuditLogs
            .Where(a => a.Timestamp > _setupTimestamp)
            .ExecuteDeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Benchmarks
    // -----------------------------------------------------------------------

    /// <summary>
    /// ❌ INSERT 1 000 audit logs with <c>Guid.NewGuid()</c> (random) clustered keys.
    /// Every row must be placed at a random position in the B-tree → page splits.
    /// </summary>
    [Benchmark(Baseline = true, Description = "❌ INSERT 1k logs — Guid.NewGuid() (random, page splits)")]
    public async Task NaiveInsert() => await _naive.InsertLogsAsync(_naiveInsertBatch);

    /// <summary>
    /// ✅ INSERT 1 000 audit logs with <c>Guid.CreateVersion7()</c> (time-ordered) clustered keys.
    /// Every row appends to the end of the B-tree → sequential I/O → no page splits.
    /// </summary>
    [Benchmark(Description = "✅ INSERT 1k logs — Guid.CreateVersion7() (sequential, no splits)")]
    public async Task OptimizedInsert() => await _optimized.InsertLogsAsync(_optimizedInsertBatch);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        // Remove all benchmark rows (warmup + iteration rows)
        await _naiveCtx.AuditLogs
            .Where(a => a.Timestamp > _setupTimestamp)
            .ExecuteDeleteAsync();
        await _optimizedCtx.AuditLogs
            .Where(a => a.Timestamp > _setupTimestamp)
            .ExecuteDeleteAsync();

        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static List<AuditLog> GenerateLogs(int count, int customerId, bool useSequential)
    {
        var actions = new[] { "Created", "Updated", "Deleted" };
        var entities = new[] { "Order", "Product", "Customer", "Address" };

        return Enumerable.Range(0, count).Select(i => new AuditLog
        {
            // ❌ Naive:     Guid.NewGuid()        → random, causes page splits
            // ✅ Optimized: Guid.CreateVersion7() → time-ordered, no page splits
            Id = useSequential ? Guid.CreateVersion7() : Guid.NewGuid(),
            EntityName          = entities[i % entities.Length],
            EntityId            = (i % 1000) + 1,
            Action              = actions[i % actions.Length],
            OldValues           = null,
            NewValues           = null,
            ChangedByCustomerId = customerId,
            Timestamp           = DateTime.UtcNow
        }).ToList();
    }
}
