using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED existence-check queries.
///
/// Improvements over the naive version:
///   1. <c>AnyAsync()</c> generates <c>SELECT CASE WHEN EXISTS(…) THEN 1 ELSE 0 END</c>.
///      SQL Server stops scanning as soon as it finds the first matching row
///      (short-circuit evaluation), whereas <c>Count()</c> must process every row.
///   2. <c>AsNoTracking()</c> — no change-tracker overhead for a boolean result.
///   3. The IX_Customers_Email index supports seeking on Email equality.
///   4. The IX_Orders_CustomerId index supports seeking on CustomerId equality.
/// </summary>
public sealed class OptimizedExistenceCheckQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Checks whether a customer with the given email exists using <c>Any()</c>.
    /// SQL: <c>SELECT CASE WHEN EXISTS (SELECT 1 FROM Customers WHERE Email = @email)
    ///       THEN 1 ELSE 0 END</c> → index seek on IX_Customers_Email, stops at first match.
    /// </summary>
    public async Task<bool> CustomerExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // ✅ Any() → EXISTS → short-circuit at first match
        // ✅ AsNoTracking (irrelevant for scalar but good habit)
        return await context.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Email == email, cancellationToken);
    }

    /// <summary>
    /// Checks whether a customer has ANY orders using <c>Any()</c>.
    /// SQL: <c>SELECT CASE WHEN EXISTS (SELECT 1 FROM Orders WHERE CustomerId = @id)
    ///       THEN 1 ELSE 0 END</c> → index seek on IX_Orders_CustomerId, stops immediately.
    /// </summary>
    public async Task<bool> CustomerHasOrdersAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        // ✅ Any() → EXISTS (stops at first match)
        // ✅ IX_Orders_CustomerId enables index seek
        return await context.Orders
            .AsNoTracking()
            .AnyAsync(o => o.CustomerId == customerId, cancellationToken);
    }
}
