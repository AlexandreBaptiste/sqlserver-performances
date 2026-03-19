namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// Represents a customer in the e-commerce system.
/// Central entity — related to <see cref="Address"/> (1-to-1) and <see cref="Order"/> (1-to-many).
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Address? Address { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
