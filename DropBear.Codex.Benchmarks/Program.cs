using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Entry point for DropBear.Codex performance benchmarks.
///     Run with: dotnet run -c Release
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks with default configuration
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator); // Allow Debug builds for development

        // Uncomment specific benchmark classes to run individually:
        // BenchmarkRunner.Run<ResultPatternBenchmarks>(config, args);
        // BenchmarkRunner.Run<SerializationBenchmarks>(config, args);
        // BenchmarkRunner.Run<HashingBenchmarks>(config, args);
        // BenchmarkRunner.Run<AsyncStreamingBenchmarks>(config, args);

        // Run all benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
