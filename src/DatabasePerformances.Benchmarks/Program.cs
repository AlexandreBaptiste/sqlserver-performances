using BenchmarkDotNet.Running;
using DatabasePerformances.Benchmarks;
using DatabasePerformances.Benchmarks.Scenarios;

// =============================================================================
// DatabasePerformances.Benchmarks
// =============================================================================
// Runs all performance benchmark scenarios comparing naive vs. optimized queries.
//
// ⚠ IMPORTANT: BenchmarkDotNet requires Release mode.
//   Run with:  dotnet run -c Release
//
// Usage:
//   dotnet run -c Release               → run ALL scenarios (interactive menu)
//   dotnet run -c Release -- --filter *CustomerSearch*  → specific scenario
//   dotnet run -c Release -- --list flat                → list all benchmarks
//
// Results are written to: BenchmarkDotNet.Artifacts/results/
// =============================================================================

Console.WriteLine("""
╔══════════════════════════════════════════════════════════════╗
║        Database Performance Benchmarks — .NET 10             ║
║  Make sure DbNaive and DbOptimized are seeded before running ║
╚══════════════════════════════════════════════════════════════╝
""");

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, new BenchmarkConfiguration());
