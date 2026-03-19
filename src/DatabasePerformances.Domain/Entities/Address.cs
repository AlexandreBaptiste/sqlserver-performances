namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// Shipping/billing address for a <see cref="Customer"/>. One-to-one relationship.
/// </summary>
public class Address
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    // Navigation property
    public Customer Customer { get; set; } = null!;
}
