using DatabasePerformances.Infrastructure;
using DatabasePerformances.Infrastructure.Naive;
using DatabasePerformances.Infrastructure.Optimized;

namespace DatabasePerformances.Tests;

/// <summary>
/// xUnit class fixture that creates and disposes both DbContexts once per
/// test class. All test classes annotated with
/// <c>[Collection(nameof(DatabaseCollection))]</c> share a single fixture
/// instance, avoiding the overhead of opening a new connection pool for each
/// test method.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public NaiveDbContext NaiveContext { get; private set; } = null!;
    public OptimizedDbContext OptimizedContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var config = DbContextFactory.BuildConfiguration();
        NaiveContext     = DbContextFactory.CreateNaive(config);
        OptimizedContext = DbContextFactory.CreateOptimized(config);

        // Verify connectivity early so failures report clearly
        await NaiveContext.Database.CanConnectAsync();
        await OptimizedContext.Database.CanConnectAsync();
    }

    public async Task DisposeAsync()
    {
        await NaiveContext.DisposeAsync();
        await OptimizedContext.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition. Test classes in this collection share one
/// <see cref="DatabaseFixture"/> instance and do NOT run in parallel with
/// each other (avoids connection pool exhaustion).
/// </summary>
[CollectionDefinition(nameof(DatabaseCollection))]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
