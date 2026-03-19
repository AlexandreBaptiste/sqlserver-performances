using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace DatabasePerformances.Benchmarks;

/// <summary>
/// BenchmarkDotNet configuration shared by all benchmark classes.
///
/// Key choices:
/// - WarmupCount = 1        → one warm-up to open connection pool / JIT
/// - IterationCount = 5     → enough to detect consistent differences
/// - MarkdownExporter       → writes GitHub-flavoured Markdown results
/// - MemoryDiagnoser        → captures allocations (Gen0/1/2, bytes) per op
/// - [Baseline = true]      → naive method is the baseline; "Ratio" column shows
///                            how much faster the optimized version is
/// </summary>
public sealed class BenchmarkConfiguration : ManualConfig
{
    public BenchmarkConfiguration()
    {
        // Run on the current .NET runtime with tighter iteration settings
        AddJob(Job.Default
            .WithWarmupCount(1)
            .WithIterationCount(5)
            .WithId("DB-Perf"));

        // Exporters — Markdown is automatically saved to BenchmarkDotNet.Artifacts/
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        // Memory allocation columns (very educational: shows allocations per operation)
        AddDiagnoser(MemoryDiagnoser.Default);

        // Extra statistical columns
        AddColumn(StatisticColumn.Min);
        AddColumn(StatisticColumn.Max);

        AddLogger(ConsoleLogger.Default);
    }
}
