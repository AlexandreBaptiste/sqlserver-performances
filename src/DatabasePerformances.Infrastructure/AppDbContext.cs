using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure;

/// <summary>
/// Shared base DbContext that applies all entity configurations.
/// Both <see cref="NaiveDbContext"/> and <see cref="OptimizedDbContext"/> extend
/// this class — the only difference between the two databases is the set of
/// indexes defined in the SQL schema scripts.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Address> Addresses { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<ProductReview> ProductReviews { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new AddressConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new ProductReviewConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
