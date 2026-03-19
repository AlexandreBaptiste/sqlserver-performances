using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Optimized;

/// <summary>
/// EF Core context bound to <c>DbOptimized</c> — the database with a full set
/// of composite, covering, filtered and columnstore indexes.
/// Same entity model as <see cref="NaiveDbContext"/>; the performance difference
/// comes entirely from the schema-level indexes and the query patterns used.
/// </summary>
public sealed class OptimizedDbContext(DbContextOptions<OptimizedDbContext> options) : AppDbContext(options);
