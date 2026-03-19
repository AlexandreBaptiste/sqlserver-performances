using DatabasePerformances.Domain.Enums;

namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// A customer purchase order. Contains one or more <see cref="OrderItem"/> lines.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
