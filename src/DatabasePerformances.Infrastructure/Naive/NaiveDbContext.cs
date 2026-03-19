using Microsoft.EntityFrameworkCore;

namespace DatabasePerformances.Infrastructure.Naive;

/// <summary>
/// EF Core context bound to <c>DbNaive</c> — the database with only primary key constraints.
/// All queries run against a schema with NO extra non-clustered indexes,
/// so every non-PK lookup is a full table scan.
/// </summary>
public sealed class NaiveDbContext(DbContextOptions<NaiveDbContext> options) : AppDbContext(options);
