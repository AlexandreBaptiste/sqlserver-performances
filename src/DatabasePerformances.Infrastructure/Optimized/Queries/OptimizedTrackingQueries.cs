using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED no-tracking queries.
///
/// Improvements over the naive version:
///   1. <c>AsNoTracking()</c> disables the change tracker entirely.
///      No snapshot arrays, no identity maps, no state entries.
///      For large result sets this halves both memory allocation and CPU time.
///   2. A lightweight DTO projection (Select) fetches only the columns the
///      caller needs — further reducing data transfer and materialization cost.
/// </summary>
public sealed class OptimizedTrackingQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Loads a page of customers WITHOUT change-tracking and with projection.
    /// <c>AsNoTracking()</c> + <c>Select</c> → no snapshot, no identity map,
    /// minimal memory allocation.
    /// </summary>
    public async Task<List<CustomerSearchResult>> GetCustomersNoTrackingAsync(
        int take = 5000,
        CancellationToken cancellationToken = default)
    {
        // ✅ AsNoTracking() → zero tracker overhead
        // ✅ Projection → only 4 columns, not SELECT *
        return await context.Customers
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Take(take)
            .Select(c => new CustomerSearchResult(
                c.Id, c.FirstName, c.LastName, c.Email))
            .ToListAsync(cancellationToken);
    }
}
