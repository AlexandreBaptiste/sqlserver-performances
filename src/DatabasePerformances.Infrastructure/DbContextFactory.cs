using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Optimized;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DatabasePerformances.Infrastructure;

/// <summary>
/// Convenience factory that creates <see cref="NaiveDbContext"/> and
/// <see cref="OptimizedDbContext"/> from a loaded <see cref="IConfiguration"/>.
/// Used by the Benchmarks and Tests projects to avoid boilerplate setup code.
/// </summary>
public static class DbContextFactory
{
    /// <summary>Creates a <see cref="NaiveDbContext"/> using the connection string
    /// <c>ConnectionStrings:NaiveDatabase</c> from configuration.</summary>
    public static NaiveDbContext CreateNaive(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NaiveDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'NaiveDatabase' is missing from configuration.");

        var options = new DbContextOptionsBuilder<NaiveDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NaiveDbContext(options);
    }

    /// <summary>Creates an <see cref="OptimizedDbContext"/> using the connection string
    /// <c>ConnectionStrings:OptimizedDatabase</c> from configuration.</summary>
    public static OptimizedDbContext CreateOptimized(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OptimizedDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'OptimizedDatabase' is missing from configuration.");

        var options = new DbContextOptionsBuilder<OptimizedDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OptimizedDbContext(options);
    }

    /// <summary>Builds an <see cref="IConfiguration"/> from <c>appsettings.json</c>
    /// in the application base directory plus environment-variable overrides.</summary>
    public static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
}
