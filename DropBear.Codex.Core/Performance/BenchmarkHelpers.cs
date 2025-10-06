#region

using System.Diagnostics;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Core.Performance;

/// <summary>
///     Helpers for performance testing and benchmarking.
///     Use these to validate optimization improvements.
/// </summary>
public static class BenchmarkHelpers
{
    /// <summary>
    ///     Measures the time taken to execute an operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // Prevent inlining for accurate measurement
    public static TimeSpan Measure(Action operation, int iterations = 1000)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Warm up
        operation();

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            operation();
        }

        sw.Stop();
        return sw.Elapsed;
    }

    /// <summary>
    ///     Measures memory allocation for an operation.
    /// </summary>
    public static long MeasureAllocations(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetTotalMemory(true);
        operation();
        var after = GC.GetTotalMemory(false);

        return Math.Max(0, after - before);
    }

    /// <summary>
    ///     Compares two implementations and reports performance difference.
    /// </summary>
    public static (TimeSpan Original, TimeSpan Optimized, double SpeedupFactor) Compare(
        Action original,
        Action optimized,
        int iterations = 1000)
    {
        var originalTime = Measure(original, iterations);
        var optimizedTime = Measure(optimized, iterations);

        var speedup = originalTime.TotalMilliseconds / optimizedTime.TotalMilliseconds;

        return (originalTime, optimizedTime, speedup);
    }
}
