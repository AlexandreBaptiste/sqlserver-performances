using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive.Queries;

/// <summary>
/// ❌ NAIVE existence-check queries.
///
/// Anti-patterns demonstrated:
///   1. <c>Count() &gt; 0</c> forces SQL Server to scan and count ALL matching
///      rows before the comparison, even though we only need to know if at least
///      one exists.  On large result sets this is dramatically slower than
///      an optimized short-circuit check.
///   2. No <c>AsNoTracking()</c> — change tracker overhead on a read-only check.
/// </summary>
public sealed class NaiveExistenceCheckQueries(NaiveDbContext context)
{
    /// <summary>
    /// Checks whether a customer with the given email exists using <c>Count()</c>.
    /// SQL: <c>SELECT COUNT(*) FROM Customers WHERE Email = @email</c> → scans
    /// all matching rows even though we only need a boolean answer.
    /// </summary>
    public async Task<bool> CustomerExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // ❌ Count() scans all matching rows then compares with 0
        // ❌ No AsNoTracking()
        return await context.Customers
            .Where(c => c.Email == email)
            .CountAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// Checks whether a customer has ANY orders using <c>Count()</c>.
    /// SQL: <c>SELECT COUNT(*) FROM Orders WHERE CustomerId = @id</c> → counts
    /// all rows even though we only need to know if there's at least one.
    /// </summary>
    public async Task<bool> CustomerHasOrdersAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        // ❌ Count() on a FK column without index → full scan + count all rows
        return await context.Orders
            .Where(o => o.CustomerId == customerId)
            .CountAsync(cancellationToken) > 0;
    }
}
