using Bogus;
using DatabasePerformances.Domain.Entities;
using DatabasePerformances.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DatabasePerformances.Seeder;

/// <summary>
/// Generates and inserts realistic e-commerce data into a target database.
/// Uses <c>SqlBulkCopy</c> for maximum throughput — inserting 200k customers
/// + 3M order items in a few minutes rather than hours.
///
/// The seeder is <b>idempotent</b>: if the target database already contains
/// data it will skip seeding and report the existing row counts.
/// </summary>
public sealed class DataSeeder(string connectionString, SeederOptions options)
{
    private readonly Faker _faker = new("en");

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>Seeds all tables in dependency order.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  Target: {MaskPassword(connectionString)}");

        if (await IsAlreadySeededAsync(cancellationToken))
        {
            Console.WriteLine("  ✔ Database already seeded. Skipping.");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var categoryIds = await SeedCategoriesAsync(cancellationToken);
        Console.WriteLine($"  ✔ {categoryIds.Count} categories inserted ({sw.ElapsedMilliseconds} ms)");

        var productIds = await SeedProductsAsync(categoryIds, cancellationToken);
        Console.WriteLine($"  ✔ {productIds.Count} products inserted ({sw.ElapsedMilliseconds} ms)");

        await SeedCustomersOrdersAndItemsAsync(productIds, cancellationToken);

        Console.WriteLine($"\n  ✔ Seeding complete in {sw.Elapsed.TotalSeconds:F1} s");
    }

    // -----------------------------------------------------------------------
    // Seeding methods
    // -----------------------------------------------------------------------

    private async Task<List<int>> SeedCategoriesAsync(CancellationToken ct)
    {
        // Root categories (no parent)
        var roots = Enumerable.Range(0, options.CategoryCount / 2)
            .Select(_ => new Category { Name = _faker.Commerce.Department() })
            .ToList();

        var rootIds = await BulkInsertCategoriesAsync(roots, parentIds: null, ct);

        // Sub-categories
        var subs = Enumerable.Range(0, options.CategoryCount / 2)
            .Select(i => new Category
            {
                Name = _faker.Commerce.Department(),
                ParentCategoryId = rootIds[i % rootIds.Count]
            })
            .ToList();

        var subIds = await BulkInsertCategoriesAsync(subs, parentIds: rootIds, ct);
        return [.. rootIds, .. subIds];
    }

    private async Task<List<int>> SeedProductsAsync(List<int> categoryIds, CancellationToken ct)
    {
        var table = BuildProductTable();
        var faker = new Faker<Product>("en")
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => f.Finance.Amount(1, 2000))
            .RuleFor(p => p.Stock, f => f.Random.Int(0, 1000))
            .RuleFor(p => p.CategoryId, f => f.PickRandom(categoryIds));

        var products = faker.Generate(options.ProductCount);
        foreach (var p in products)
            table.Rows.Add(p.Name, p.Description, p.Price, p.Stock, p.CategoryId);

        await BulkCopyAsync("dbo.Products", table, ct);

        // Read back generated IDs
        return await ExecuteScalarListAsync<int>(
            "SELECT Id FROM dbo.Products ORDER BY Id", null, ct);
    }

    private async Task SeedCustomersOrdersAndItemsAsync(
        List<int> productIds, CancellationToken ct)
    {
        int totalCustomers = 0, totalOrders = 0, totalItems = 0;
        var orderStatuses = Enum.GetValues<OrderStatus>();

        for (int batchStart = 0; batchStart < options.CustomerCount; batchStart += options.BatchSize)
        {
            int batchSize = Math.Min(options.BatchSize, options.CustomerCount - batchStart);
            ct.ThrowIfCancellationRequested();

            // 1. Generate and insert a batch of customers + addresses
            var customerTable = BuildCustomerTable();
            var addressTable  = BuildAddressTable(placeholder: true);

            var custFaker = new Faker<Customer>("en")
                .RuleFor(c => c.FirstName, f => f.Name.FirstName())
                .RuleFor(c => c.LastName,  f => f.Name.LastName())
                .RuleFor(c => c.Email,     f => f.Internet.Email())
                .RuleFor(c => c.Phone,     f => f.Phone.PhoneNumber())
                .RuleFor(c => c.CreatedAt, f => f.Date.Past(3));

            var customers = custFaker.Generate(batchSize);
            foreach (var c in customers)
                customerTable.Rows.Add(c.FirstName, c.LastName, c.Email,
                                       (object?)c.Phone ?? DBNull.Value, c.CreatedAt);

            await BulkCopyAsync("dbo.Customers", customerTable, ct);
            totalCustomers += batchSize;

            // Read back the just-inserted customer IDs (last N rows)
            var customerIds = await ExecuteScalarListAsync<int>(
                "SELECT TOP (@Count) Id FROM dbo.Customers ORDER BY Id DESC",
                new SqlParameter("@Count", batchSize), ct);
            customerIds.Reverse(); // ascending order

            // 2. Insert addresses (1 per customer)
            var addrTable = BuildAddressTable();
            var addrFaker = new Faker("en");
            foreach (var customerId in customerIds)
            {
                addrTable.Rows.Add(
                    customerId,
                    addrFaker.Address.StreetAddress(),
                    addrFaker.Address.City(),
                    addrFaker.Address.ZipCode(),
                    addrFaker.Address.Country());
            }
            await BulkCopyAsync("dbo.Addresses", addrTable, ct);

            // 3. Generate and insert orders
            var orderTable = BuildOrderTable();
            var orderFaker = new Faker("en");
            var orderBatch = new List<(int customerId, int tempIdx)>();

            foreach (var customerId in customerIds)
            {
                for (int o = 0; o < options.OrdersPerCustomer; o++)
                {
                    orderTable.Rows.Add(
                        customerId,
                        orderFaker.Date.Past(2),
                        (byte)orderFaker.PickRandom(orderStatuses),
                        orderFaker.Finance.Amount(10, 5000));
                    orderBatch.Add((customerId, orderTable.Rows.Count - 1));
                }
            }

            await BulkCopyAsync("dbo.Orders", orderTable, ct);
            int ordersInBatch = orderBatch.Count;
            totalOrders += ordersInBatch;

            // Read back the just-inserted order IDs
            var orderIds = await ExecuteScalarListAsync<int>(
                "SELECT TOP (@Count) Id FROM dbo.Orders ORDER BY Id DESC",
                new SqlParameter("@Count", ordersInBatch), ct);
            orderIds.Reverse();

            // 4. Generate and insert order items
            var itemTable     = BuildOrderItemTable();
            var itemFaker     = new Faker("en");

            foreach (var orderId in orderIds)
            {
                for (int i = 0; i < options.ItemsPerOrder; i++)
                {
                    var productId = itemFaker.PickRandom(productIds);
                    var qty = itemFaker.Random.Int(1, 10);
                    var price = itemFaker.Finance.Amount(1, 500);
                    itemTable.Rows.Add(orderId, productId, qty, price);
                }
            }

            await BulkCopyAsync("dbo.OrderItems", itemTable, ct);
            totalItems += orderIds.Count * options.ItemsPerOrder;

            // Progress report
            int pct = (int)((double)totalCustomers / options.CustomerCount * 100);
            Console.Write($"\r  → {totalCustomers:N0}/{options.CustomerCount:N0} customers " +
                          $"| {totalOrders:N0} orders | {totalItems:N0} items  [{pct}%]");
        }

        Console.WriteLine(); // flush progress line
        Console.WriteLine($"  ✔ {totalCustomers:N0} customers, " +
                          $"{totalOrders:N0} orders, {totalItems:N0} items inserted.");
    }

    // -----------------------------------------------------------------------
    // SqlBulkCopy helpers
    // -----------------------------------------------------------------------

    private async Task BulkCopyAsync(string tableName, DataTable table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = tableName,
            BatchSize = 1_000,
            BulkCopyTimeout = 300
        };

        // Map every column by name so order doesn't matter
        foreach (DataColumn col in table.Columns)
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

        await bulk.WriteToServerAsync(table, ct);
    }

    private async Task<List<int>> BulkInsertCategoriesAsync(
        List<Category> categories,
        List<int>? parentIds,
        CancellationToken ct)
    {
        var table = new DataTable();
        table.Columns.Add("Name",             typeof(string));
        table.Columns.Add("ParentCategoryId", typeof(int));

        foreach (var cat in categories)
            table.Rows.Add(cat.Name, (object?)cat.ParentCategoryId ?? DBNull.Value);

        await BulkCopyAsync("dbo.Categories", table, ct);

        return await ExecuteScalarListAsync<int>(
            "SELECT TOP (@Count) Id FROM dbo.Categories ORDER BY Id DESC",
            new SqlParameter("@Count", categories.Count), ct);
    }

    private async Task<List<T>> ExecuteScalarListAsync<T>(
        string sql, SqlParameter? parameter, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        if (parameter is not null)
            cmd.Parameters.Add(parameter);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new List<T>();
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetFieldValue<T>(0));

        return result;
    }

    private async Task<bool> IsAlreadySeededAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Customers", conn);
        var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        return count > 0;
    }

    // -----------------------------------------------------------------------
    // DataTable builders — match the SQL schema column-for-column
    // -----------------------------------------------------------------------

    private static DataTable BuildCustomerTable()
    {
        var t = new DataTable();
        t.Columns.Add("FirstName", typeof(string));
        t.Columns.Add("LastName",  typeof(string));
        t.Columns.Add("Email",     typeof(string));
        t.Columns.Add("Phone",     typeof(string));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        return t;
    }

    private static DataTable BuildAddressTable(bool placeholder = false)
    {
        var t = new DataTable();
        t.Columns.Add("CustomerId", typeof(int));
        t.Columns.Add("Street",     typeof(string));
        t.Columns.Add("City",       typeof(string));
        t.Columns.Add("PostalCode", typeof(string));
        t.Columns.Add("Country",    typeof(string));
        return t;
    }

    private static DataTable BuildProductTable()
    {
        var t = new DataTable();
        t.Columns.Add("Name",        typeof(string));
        t.Columns.Add("Description", typeof(string));
        t.Columns.Add("Price",       typeof(decimal));
        t.Columns.Add("Stock",       typeof(int));
        t.Columns.Add("CategoryId",  typeof(int));
        return t;
    }

    private static DataTable BuildOrderTable()
    {
        var t = new DataTable();
        t.Columns.Add("CustomerId",  typeof(int));
        t.Columns.Add("OrderDate",   typeof(DateTime));
        t.Columns.Add("Status",      typeof(byte));
        t.Columns.Add("TotalAmount", typeof(decimal));
        return t;
    }

    private static DataTable BuildOrderItemTable()
    {
        var t = new DataTable();
        t.Columns.Add("OrderId",   typeof(int));
        t.Columns.Add("ProductId", typeof(int));
        t.Columns.Add("Quantity",  typeof(int));
        t.Columns.Add("UnitPrice", typeof(decimal));
        return t;
    }

    private static string MaskPassword(string cs)
    {
        // Replace the password value in the connection string for safe logging
        var idx = cs.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return cs;
        var end = cs.IndexOf(';', idx);
        var passwordSection = end < 0 ? cs[idx..] : cs[idx..end];
        return cs.Replace(passwordSection, "Password=*****");
    }
}

/// <summary>Options read from <c>appsettings.json</c> Seeder section.</summary>
public sealed record SeederOptions
{
    public int CategoryCount     { get; init; } = 20;
    public int ProductCount      { get; init; } = 5_000;
    public int CustomerCount     { get; init; } = 200_000;
    public int OrdersPerCustomer { get; init; } = 5;
    public int ItemsPerOrder     { get; init; } = 3;
    public int BatchSize         { get; init; } = 500;
}
