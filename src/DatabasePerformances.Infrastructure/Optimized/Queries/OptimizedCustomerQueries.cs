using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED customer search queries.
///
/// Improvements over the naive version:
///   1. <c>StartsWith()</c> generates <c>LIKE 'term%'</c> — a prefix-only
///      wildcard that allows SQL Server to perform an index seek on
///      <c>IX_Customers_Email</c> instead of a full table scan.
///   2. <c>AsNoTracking()</c> skips the EF Core change-tracker entirely for
///      read-only queries, reducing CPU and memory pressure.
///   3. A <c>Select</c> projection returns only the columns the caller needs,
///      so SQL Server fetches far fewer pages (covering index).
///   4. A compiled query (<see cref="EF.CompileAsyncQuery"/>) pre-parses the
///      LINQ expression tree once; subsequent calls skip expression translation.
/// </summary>
public sealed class OptimizedCustomerQueries(OptimizedDbContext context)
{
    // ✅ Compiled query: expression tree is translated to SQL exactly once.
    //    Eliminates LINQ-to-SQL compilation overhead on every invocation.
    private static readonly Func<OptimizedDbContext, string, IAsyncEnumerable<CustomerSearchResult>>
        _emailPrefixQuery = EF.CompileAsyncQuery(
            (OptimizedDbContext ctx, string prefix) =>
                ctx.Customers
                    .AsNoTracking()
                    .Where(c => c.Email.StartsWith(prefix))
                    .Select(c => new CustomerSearchResult(
                        c.Id, c.FirstName, c.LastName, c.Email)));

    /// <summary>
    /// Searches customers by email prefix.
    /// SQL: <c>WHERE Email LIKE 'term%'</c> → index seek on <c>IX_Customers_Email</c>.
    /// </summary>
    public async Task<List<CustomerSearchResult>> SearchByEmailPrefixAsync(
        string emailPrefix,
        CancellationToken cancellationToken = default)
    {
        // ✅ StartsWith() → LIKE 'prefix%' → index seek, not scan
        // ✅ AsNoTracking() on compiled query — no change-tracker overhead
        // ✅ Projection — only Id, FirstName, LastName, Email fetched
        var results = new List<CustomerSearchResult>();

        await foreach (var item in _emailPrefixQuery(context, emailPrefix)
                           .WithCancellation(cancellationToken))
        {
            results.Add(item);
        }

        return results;
    }

    /// <summary>
    /// Searches customers by last name prefix.
    /// SQL: <c>WHERE LastName LIKE 'term%'</c> → uses <c>IX_Customers_LastName_FirstName</c>.
    /// </summary>
    public async Task<List<CustomerSearchResult>> SearchByLastNamePrefixAsync(
        string lastNamePrefix,
        CancellationToken cancellationToken = default)
    {
        // ✅ StartsWith → prefix LIKE, uses composite index
        // ✅ AsNoTracking + minimal projection
        return await context.Customers
            .AsNoTracking()
            .Where(c => c.LastName.StartsWith(lastNamePrefix))
            .Select(c => new CustomerSearchResult(
                c.Id, c.FirstName, c.LastName, c.Email))
            .ToListAsync(cancellationToken);
    }
}
