namespace DatabasePerformances.Infrastructure.Dtos;

// =============================================================================
// Query result DTOs — plain records used as return types for all query classes.
// Using records gives structural equality for free, which simplifies testing.
// =============================================================================

/// <summary>Lightweight customer projection for search results.</summary>
public record CustomerSearchResult(int Id, string FirstName, string LastName, string Email);

/// <summary>Summary of an order, used in order history and pagination results.</summary>
public record OrderSummary(int Id, DateTime OrderDate, string Status, decimal TotalAmount, int ItemCount);

/// <summary>A single line in an order, including product name.</summary>
public record OrderLineDetail(int OrderItemId, int ProductId, string ProductName, int Quantity, decimal UnitPrice);

/// <summary>Full order with all its line items — used in complex-join scenario.</summary>
public record OrderWithLines(OrderSummary Order, IReadOnlyList<OrderLineDetail> Lines);

/// <summary>Product in the catalog, filtered by category and price.</summary>
public record ProductCatalogItem(int Id, string Name, string CategoryName, decimal Price, int Stock);

/// <summary>Revenue aggregation per product for a time window.</summary>
public record ProductRevenueItem(int ProductId, string ProductName, string CategoryName, decimal TotalRevenue, int TotalQuantitySold);

/// <summary>Monthly sales roll-up per category.</summary>
public record MonthlySalesItem(int Year, int Month, string CategoryName, decimal TotalRevenue, int OrderCount);

/// <summary>A page of orders returned by keyset (optimized) or offset (naive) pagination.</summary>
public record OrderPage(IReadOnlyList<OrderSummary> Items, int? NextCursorId, int PageNumber);

/// <summary>Bulk-insert result metadata.</summary>
public record BulkInsertResult(int RowsInserted, long ElapsedMilliseconds);
