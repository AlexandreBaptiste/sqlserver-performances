using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE audit log queries — uses <see cref="Guid.NewGuid"/> to generate primary keys.
///
/// Anti-pattern demonstrated (Scenario 12):
///   <c>Guid.NewGuid()</c> produces cryptographically random 128-bit values with no
///   relationship to insertion order.  Because the clustered index is ordered by the PK,
///   every INSERT must find a <em>random</em> position inside the B-tree:
///   <list type="bullet">
///     <item>~50 % of pages are split to make room for the new row.</item>
///     <item>Page fill factor drops → more pages → more I/O for every query.</item>
///     <item>Fragmentation accumulates until an index rebuild is performed.</item>
///     <item>Write amplification: a single logical INSERT triggers multiple physical page writes.</item>
///   </list>
///   This issue is invisible in the application code — the slowdown is entirely caused
///   by the GUID generation strategy.
/// </summary>
public sealed class NaiveGuidKeyQueries(NaiveDbContext context)
{
    /// <summary>
    /// Inserts <paramref name="count"/> audit log entries using <see cref="Guid.NewGuid"/>.
    /// <para>
    /// ❌ Every ID is randomly distributed across the entire GUID space.
    ///    SQL Server must insert each row at a random position in the clustered B-tree,
    ///    causing ~50 % page-split rate and severe index fragmentation.
    /// </para>
    /// </summary>
    public async Task InsertLogsAsync(
        IReadOnlyList<AuditLog> logs,
        CancellationToken cancellationToken = default)
    {
        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Retrieves the most recent audit entries for a given entity name.</summary>
    public async Task<List<AuditLogEntry>> GetRecentByEntityAsync(
        string entityName,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == entityName)
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new AuditLogEntry(a.Id, a.EntityName, a.EntityId, a.Action, a.Timestamp))
            .ToListAsync(cancellationToken);
    }
}
