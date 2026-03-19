using DatabasePerformances.Seeder;
using Microsoft.Extensions.Configuration;

// =============================================================================
// DatabasePerformances.Seeder
// =============================================================================
// Generates realistic e-commerce data and bulk-inserts it into both databases.
//
// Usage:
//   dotnet run                       → seed DbNaive AND DbOptimized (full dataset)
//   dotnet run -- --target naive     → seed DbNaive only
//   dotnet run -- --target optimized → seed DbOptimized only
//   dotnet run -- --quick            → use reduced dataset (~10 % of full size)
//
// The seeder is idempotent: re-running on an already-seeded database is a no-op.
// =============================================================================

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// Parse CLI flags
bool seedNaive     = !args.Contains("--target") || args.Contains("naive");
bool seedOptimized = !args.Contains("--target") || args.Contains("optimized");
bool quick         = args.Contains("--quick");

// Load seeder options — override counts when --quick is requested
var opts = config.GetSection("Seeder").Get<SeederOptions>() ?? new SeederOptions();
if (quick)
{
    opts = opts with
    {
        CustomerCount     = 10_000,
        ProductCount      = 500,
        OrdersPerCustomer = 5,
        ItemsPerOrder     = 3,
        BatchSize         = 200
    };
    Console.WriteLine("⚡ Quick mode: reduced dataset (10k customers).");
}

int exitCode = 0;

if (seedNaive)
{
    var cs = config.GetConnectionString("NaiveDatabase")
        ?? throw new InvalidOperationException("NaiveDatabase connection string missing.");

    Console.WriteLine("\n🌱 Seeding DbNaive …");
    try
    {
        await new DataSeeder(cs, opts).SeedAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  ❌ DbNaive seeding failed: {ex.Message}");
        exitCode = 1;
    }
}

if (seedOptimized)
{
    var cs = config.GetConnectionString("OptimizedDatabase")
        ?? throw new InvalidOperationException("OptimizedDatabase connection string missing.");

    Console.WriteLine("\n🌱 Seeding DbOptimized …");
    try
    {
        await new DataSeeder(cs, opts).SeedAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  ❌ DbOptimized seeding failed: {ex.Message}");
        exitCode = 1;
    }
}

Console.WriteLine("\nDone.");
return exitCode;
