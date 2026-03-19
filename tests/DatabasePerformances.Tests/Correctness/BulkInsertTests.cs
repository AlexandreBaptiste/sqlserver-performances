using Bogus;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Optimized;
using DatabasePerformances.Infrastructure.Optimized.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabasePerformances.Tests.Correctness;

/// <summary>
/// Correctness tests for Scenario 8 — Bulk Insert.
/// Verifies that both <c>AddRange</c> and <c>SqlBulkCopy</c> approaches
/// insert the expected number of rows.
///
/// Each test uses a unique email prefix to isolate inserted rows, and cleans
/// up after itself so other tests are unaffected.
/// </summary>
[Collection(nameof(DatabaseCollection))]
#pragma warning disable CS9113 // Parameter '_' is not read — required by xUnit collection fixture injection
public sealed class BulkInsertTests(DatabaseFixture _) : IAsyncLifetime
#pragma warning restore CS9113
{
    private OptimizedDbContext _ctx = null!;
    private int _maxIdBefore;

    public async Task InitializeAsync()
    {
        // Use a separate context so AddRange tracking doesn't leak
        var config = DbContextFactory.BuildConfiguration();
        _ctx = DbContextFactory.CreateOptimized(config);
        _maxIdBefore = await _ctx.Customers.MaxAsync(c => (int?)c.Id) ?? 0;
    }

    public async Task DisposeAsync()
    {
        // Clean up rows inserted during these tests
        await _ctx.Customers
            .Where(c => c.Id > _maxIdBefore)
            .ExecuteDeleteAsync();
        await _ctx.DisposeAsync();
    }

    [Fact(DisplayName = "AddRange inserts all rows correctly")]
    public async Task AddRange_InsertsCorrectCount()
    {
        const int count = 50;
        var customers = GenerateCustomers(count, "addrange_test_");

        var queries = new OptimizedBulkInsertQueries(_ctx);
        await queries.InsertWithAddRangeAsync(customers);

        var inserted = await _ctx.Customers
            .AsNoTracking()
            .Where(c => c.Id > _maxIdBefore)
            .CountAsync();

        Assert.Equal(count, inserted);
    }

    [Fact(DisplayName = "BulkCopy inserts all rows correctly")]
    public async Task BulkCopy_InsertsCorrectCount()
    {
        const int count = 100;
        var customers = GenerateCustomers(count, "bulkcopy_test_");

        var queries = new OptimizedBulkInsertQueries(_ctx);
        await queries.InsertWithBulkCopyAsync(customers);

        var inserted = await _ctx.Customers
            .AsNoTracking()
            .Where(c => c.Id > _maxIdBefore)
            .CountAsync();

        Assert.True(inserted >= count,
            $"Expected ≥ {count} inserted rows but found {inserted}");
    }

    private static List<Customer> GenerateCustomers(int count, string emailPrefix) =>
        new Faker<Customer>("en")
            .RuleFor(c => c.FirstName, f => f.Name.FirstName())
            .RuleFor(c => c.LastName, f => f.Name.LastName())
            .RuleFor(c => c.Email, (f, _) => emailPrefix + f.Internet.Email())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(c => c.CreatedAt, _ => DateTime.UtcNow)
            .Generate(count);
}
