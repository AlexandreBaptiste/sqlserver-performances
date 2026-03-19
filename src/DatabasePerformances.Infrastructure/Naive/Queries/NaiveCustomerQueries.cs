using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE customer search queries.
///
/// Anti-patterns demonstrated:
///   1. <c>Contains()</c> generates <c>LIKE '%term%'</c> — a leading wildcard
///      prevents ANY index from being used, even if one existed.
///   2. No <c>AsNoTracking()</c> — EF Core tracks every returned entity in the
///      change-tracker, consuming memory and CPU for data we never modify.
///   3. Returns full <see cref="Customer"/> entities (SELECT *) even though the
///      caller only needs Id, Name, and Email.
/// </summary>
public sealed class NaiveCustomerQueries(NaiveDbContext context)
{
    /// <summary>
    /// Searches customers by email using a mid-string pattern.
    /// SQL: <c>WHERE Email LIKE '%term%'</c> → full table scan on 200k rows.
    /// </summary>
    public async Task<List<Customer>> SearchByEmailAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        // ❌ Contains() → LIKE '%term%' → no index seek possible
        // ❌ No AsNoTracking()  → 200k entities tracked unnecessarily
        // ❌ Returns entire Customer entity (SELECT *)
        return await context.Customers
            .Where(c => c.Email.Contains(searchTerm))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Searches customers by last name using a mid-string pattern.
    /// SQL: <c>WHERE LastName LIKE '%term%'</c> → full table scan.
    /// </summary>
    public async Task<List<Customer>> SearchByLastNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        // ❌ Contains() → full scan, and returns full entity
        return await context.Customers
            .Where(c => c.LastName.Contains(searchTerm))
            .ToListAsync(cancellationToken);
    }
}
