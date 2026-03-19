using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE tracking queries.
///
/// Anti-patterns demonstrated:
///   1. EF Core's change tracker is enabled by default.  When loading large
///      result sets for read-only purposes, the tracker allocates identity maps,
///      snapshot arrays, and state entries for EVERY entity.  For 10 000 rows
///      this can double both memory allocation and wall-clock time.
///   2. Returns full entities (SELECT *) instead of lightweight DTOs, further
///      amplifying tracking overhead because more data is materialized.
/// </summary>
public sealed class NaiveTrackingQueries(NaiveDbContext context)
{
    /// <summary>
    /// Loads a page of customers WITH change-tracking enabled.
    /// Every entity is snapshotted, identity-resolved, and stored in the
    /// context's internal maps — all of which is wasted work for a read-only list.
    /// </summary>
    public async Task<List<Customer>> GetCustomersTrackedAsync(
        int take = 5000,
        CancellationToken cancellationToken = default)
    {
        // ❌ No AsNoTracking() → full snapshot + identity map overhead
        // ❌ Returns full Customer entity (SELECT *)
        return await context.Customers
            .OrderBy(c => c.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
