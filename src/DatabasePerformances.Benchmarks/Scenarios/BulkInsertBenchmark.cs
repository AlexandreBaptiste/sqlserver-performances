using BenchmarkDotNet.Attributes;
using Bogus;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Naive.Queries;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Benchmarks.Scenarios;

/// <summary>
/// Scenario 8 — Bulk Insert (10 000 rows)
/// ============================================================
/// Inserts 10 000 customers using three strategies.
///
/// Naive:     SaveChangesAsync() per entity  → 10 000 round-trips.
/// Optimized: AddRange + single SaveChanges  → ~240 batched round-trips.
/// Fastest:   SqlBulkCopy                   → 1 minimally-logged operation.
///
/// Expected ratios: AddRange ~40× faster than naive;
///                  BulkCopy ~200× faster than naive.
///
/// Note: An IterationSetup/Cleanup pair deletes inserted test rows so the
/// data size stays constant across iterations.
/// </summary>
[Config(typeof(BenchmarkConfiguration))]
[BenchmarkCategory("BulkInsert")]
public class BulkInsertBenchmark
{
    private NaiveDbContext _naiveCtx = null!;
    private OptimizedDbContext _optimizedCtx = null!;
    private NaiveBulkInsertQueries _naive = null!;
    private OptimizedBulkInsertQueries _optimized = null!;

    private List<Customer> _customers = null!;

    // Track the max customer ID before the benchmark started so we can clean up
    private int _naiveMaxIdBefore;
    private int _optimizedMaxIdBefore;

    private const int BatchCount = 10_000;

    [GlobalSetup]
    public async Task Setup()
    {
        var config = DbContextFactory.BuildConfiguration();
        _naiveCtx     = DbContextFactory.CreateNaive(config);
        _optimizedCtx = DbContextFactory.CreateOptimized(config);
        _naive        = new NaiveBulkInsertQueries(_naiveCtx);
        _optimized    = new OptimizedBulkInsertQueries(_optimizedCtx);

        // Record baseline max IDs for cleanup
        _naiveMaxIdBefore     = await _naiveCtx.Customers.MaxAsync(c => (int?)c.Id) ?? 0;
        _optimizedMaxIdBefore = await _optimizedCtx.Customers.MaxAsync(c => (int?)c.Id) ?? 0;
    }

    [IterationSetup]
    public void GenerateCustomers()
    {
        // Fresh batch every iteration so emails remain unique across iterations
        _customers = new Faker<Customer>("en")
            .RuleFor(c => c.FirstName, f => f.Name.FirstName())
            .RuleFor(c => c.LastName,  f => f.Name.LastName())
            .RuleFor(c => c.Email,     f => f.Internet.Email())
            .RuleFor(c => c.Phone,     f => f.Phone.PhoneNumber())
            .RuleFor(c => c.CreatedAt, _ => DateTime.UtcNow)
            .Generate(BatchCount);
    }

    [IterationCleanup]
    public async Task DeleteInsertedRows()
    {
        // Remove test rows inserted during this iteration to keep data size stable
        await _naiveCtx.Customers
            .Where(c => c.Id > _naiveMaxIdBefore)
            .ExecuteDeleteAsync();

        await _optimizedCtx.Customers
            .Where(c => c.Id > _optimizedMaxIdBefore)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// ❌ One SaveChangesAsync() per entity — 10 000 network round-trips.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Naive — SaveChanges per entity (10k round-trips)")]
    public async Task NaiveOneByOne()
        => await _naive.InsertOneByOneAsync(_customers.Take(100)); // capped at 100 for time

    /// <summary>
    /// ✅ AddRange + single SaveChanges — EF Core batches ~42 rows/statement.
    /// </summary>
    [Benchmark(Description = "Optimized — AddRange + single SaveChanges")]
    public async Task OptimizedAddRange()
    {
        // Use a fresh context to avoid tracking conflicts from previous iterations
        var ctx  = DbContextFactory.CreateOptimized(DbContextFactory.BuildConfiguration());
        var qry  = new OptimizedBulkInsertQueries(ctx);
        await qry.InsertWithAddRangeAsync(_customers);
        await ctx.DisposeAsync();
    }

    /// <summary>
    /// ✅✅ SqlBulkCopy — minimally-logged, single batch, fastest possible.
    /// </summary>
    [Benchmark(Description = "Optimized — SqlBulkCopy (single minimally-logged operation)")]
    public async Task OptimizedBulkCopy()
    {
        var ctx = DbContextFactory.CreateOptimized(DbContextFactory.BuildConfiguration());
        var qry = new OptimizedBulkInsertQueries(ctx);
        await qry.InsertWithBulkCopyAsync(_customers);
        await ctx.DisposeAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _naiveCtx.DisposeAsync();
        await _optimizedCtx.DisposeAsync();
    }
}
