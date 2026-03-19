using DatabasePerformances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE bulk insert — inserts rows one by one, committing after each entity.
///
/// Anti-patterns demonstrated:
///   1. Calling <c>SaveChangesAsync()</c> inside a loop — each call creates a
///      separate SQL transaction and network round-trip, making N inserts = N
///      database transactions.  For 10 000 rows that is 10 000 round-trips.
///   2. No batching — the EF Core change tracker grows unboundedly.
///   3. Auto-generated PKs are fetched back individually for each row.
/// </summary>
public sealed class NaiveBulkInsertQueries(NaiveDbContext context)
{
    /// <summary>
    /// Inserts <paramref name="customers"/> one entity at a time, each with its
    /// own <c>SaveChangesAsync</c> call — worst-case bulk insert pattern.
    /// </summary>
    public async Task InsertOneByOneAsync(
        IEnumerable<Customer> customers,
        CancellationToken cancellationToken = default)
    {
        foreach (var customer in customers)
        {
            // ❌ One INSERT + COMMIT per row → N round-trips for N rows
            context.Customers.Add(customer);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
