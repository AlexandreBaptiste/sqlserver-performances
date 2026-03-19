namespace DatabasePerformances.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of an order.
/// Stored as <c>TINYINT</c> in the database.
/// </summary>
public enum OrderStatus : byte
{
    Pending    = 0,
    Processing = 1,
    Shipped    = 2,
    Delivered  = 3,
    Cancelled  = 4
}
