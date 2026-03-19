namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// A customer review for a product.
/// Used in Scenario 11 to demonstrate the write overhead of over-indexed tables.
/// </summary>
public class ProductReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int CustomerId { get; set; }

    /// <summary>Star rating from 1 (worst) to 5 (best).</summary>
    public byte Rating { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int HelpfulVotes { get; set; }
    public bool IsVerifiedPurchase { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
