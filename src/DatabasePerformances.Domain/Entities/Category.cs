namespace DatabasePerformances.Domain.Entities;

/// <summary>
/// Represents a product category. Supports a single level of parent/child hierarchy.
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Null for root categories; set for subcategories.</summary>
    public int? ParentCategoryId { get; set; }

    // Navigation properties
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
