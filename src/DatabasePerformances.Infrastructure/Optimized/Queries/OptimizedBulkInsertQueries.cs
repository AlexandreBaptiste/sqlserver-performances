using DatabasePerformances.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DatabasePerformances.Infrastructure.Optimized.Queries;

/// <summary>
/// ✅ OPTIMIZED bulk insert strategies.
///
/// Two approaches shown (from fast to fastest):
///   1. <b>EF Core AddRange + single SaveChanges</b> — batches all inserts into
///      a small number of round-trips (controlled by <c>MaxBatchSize</c>).
///   2. <b>SqlBulkCopy</b> — bypasses EF Core entirely and streams rows directly
///      to SQL Server's bulk-insert interface; fastest possible for large volumes.
/// </summary>
public sealed class OptimizedBulkInsertQueries(OptimizedDbContext context)
{
    /// <summary>
    /// Inserts customers using EF Core <c>AddRange</c> + a single <c>SaveChangesAsync</c>.
    /// EF Core automatically batches the INSERT statements (default batch size ~42
    /// for SQL Server) instead of one statement per row.
    /// </summary>
    public async Task InsertWithAddRangeAsync(
        IEnumerable<Customer> customers,
        CancellationToken cancellationToken = default)
    {
        // ✅ Single SaveChangesAsync → EF Core batches INSERTs automatically
        // ✅ Much fewer round-trips than calling SaveChanges per entity
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts customers using <c>SqlBulkCopy</c> — the fastest SQL Server
    /// bulk insert path; bypasses EF Core, transactions and row-by-row logging.
    /// </summary>
    public async Task InsertWithBulkCopyAsync(
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default)
    {
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string is null.");

        // Build an in-memory DataTable matching the Customers table schema
        using var table = new DataTable();
        table.Columns.Add("FirstName", typeof(string));
        table.Columns.Add("LastName",  typeof(string));
        table.Columns.Add("Email",     typeof(string));
        table.Columns.Add("Phone",     typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));

        foreach (var c in customers)
        {
            table.Rows.Add(c.FirstName, c.LastName, c.Email,
                           (object?)c.Phone ?? DBNull.Value, c.CreatedAt);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // ✅ SqlBulkCopy streams all rows in a single minimally-logged operation
        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "dbo.Customers",
            BatchSize = 1_000,
            BulkCopyTimeout = 120
        };

        bulk.ColumnMappings.Add("FirstName", "FirstName");
        bulk.ColumnMappings.Add("LastName",  "LastName");
        bulk.ColumnMappings.Add("Email",     "Email");
        bulk.ColumnMappings.Add("Phone",     "Phone");
        bulk.ColumnMappings.Add("CreatedAt", "CreatedAt");

        await bulk.WriteToServerAsync(table, cancellationToken);
    }
}
