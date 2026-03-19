namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// An audit trail entry recording a change made to any entity in the system.
/// Used in Scenario 12 to demonstrate the write overhead of random GUID primary keys.
///
/// Key insight:
///   Both <c>NaiveGuidKeyQueries</c> and <c>OptimizedGuidKeyQueries</c> work with
///   this same type. The only difference is HOW the <c>Id</c> GUID is generated:
///   <list type="bullet">
///     <item><b>Naive:</b> <c>Guid.NewGuid()</c> — cryptographically random → clustered B-tree
///           must insert in a random position → ~50% page splits → high fragmentation.</item>
///     <item><b>Optimized:</b> <c>Guid.CreateVersion7()</c> — time-ordered (.NET 9+) →
///           always appends to the end of the B-tree → no page splits → sequential I/O.</item>
///   </list>
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Name of the entity type that was changed (e.g. "Order", "Product").</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity record.</summary>
    public int EntityId { get; set; }

    /// <summary>The operation performed: "Created", "Updated", or "Deleted".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional JSON snapshot of the entity state before the change.</summary>
    public string? OldValues { get; set; }

    /// <summary>Optional JSON snapshot of the entity state after the change.</summary>
    public string? NewValues { get; set; }

    /// <summary>FK to the customer (user) who triggered the change.</summary>
    public int ChangedByCustomerId { get; set; }

    public DateTime Timestamp { get; set; }

    // Navigation property
    public Customer ChangedBy { get; set; } = null!;
}
