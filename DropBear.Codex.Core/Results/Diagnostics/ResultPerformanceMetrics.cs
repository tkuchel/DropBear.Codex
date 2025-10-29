#region

using System.Collections.Frozen;
using System.Diagnostics;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Represents performance metrics for a Result instance.
///     Optimized for .NET 9 with enhanced diagnostics.
/// </summary>
[DebuggerDisplay("{Summary}")]
public readonly record struct ResultPerformanceMetrics
{
    /// <summary>
    ///     Initializes a new instance of ResultPerformanceMetrics.
    /// </summary>
    /// <param name="executionTime">The time elapsed during result execution.</param>
    /// <param name="exceptionCount">The number of exceptions that occurred.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="resultType">The type name of the result.</param>
    public ResultPerformanceMetrics(
        TimeSpan executionTime,
        int exceptionCount,
        ResultState state,
        string resultType)
    {
        ExecutionTime = executionTime;
        ExceptionCount = exceptionCount;
        State = state;
        ResultType = resultType;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the time elapsed since the result was created.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    ///     Gets the number of exceptions associated with the result.
    /// </summary>
    public int ExceptionCount { get; init; }

    /// <summary>
    ///     Gets the state of the result.
    /// </summary>
    public ResultState State { get; init; }

    /// <summary>
    ///     Gets the type name of the result.
    /// </summary>
    public string ResultType { get; init; }

    /// <summary>
    ///     Gets the timestamp when these metrics were captured.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Indicates whether this result completed within acceptable performance bounds.
    ///     Default threshold: 1 second execution time and max 1 exception.
    /// </summary>
    public bool IsPerformant => ExecutionTime < TimeSpan.FromSeconds(1) && ExceptionCount <= 1;

    /// <summary>
    ///     Indicates whether this result is slow (over 500ms).
    /// </summary>
    public bool IsSlow => ExecutionTime > TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Indicates whether this result is very slow (over 2 seconds).
    /// </summary>
    public bool IsVerySlow => ExecutionTime > TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Gets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs => ExecutionTime.TotalMilliseconds;

    /// <summary>
    ///     Gets a performance rating from 0-100 (100 = excellent, 0 = poor).
    /// </summary>
    /// <remarks>
    ///     The score is calculated by starting at 100 and deducting points for:
    ///     <list type="bullet">
    ///         <item>Execution time: 5-50 points based on duration</item>
    ///         <item>Exceptions: 10 points per exception</item>
    ///         <item>Failure state: 20 points</item>
    ///         <item>Partial success: 10 points</item>
    ///     </list>
    /// </remarks>
    public int PerformanceScore
    {
        get
        {
            var score = 100;

            // Deduct points for execution time
            if (ExecutionTimeMs > 2000)
            {
                score -= 50;
            }
            else if (ExecutionTimeMs > 1000)
            {
                score -= 30;
            }
            else if (ExecutionTimeMs > 500)
            {
                score -= 15;
            }
            else if (ExecutionTimeMs > 100)
            {
                score -= 5;
            }

            // Deduct points for exceptions
            score -= ExceptionCount * 10;

            // Deduct points for failure state
            if (State == ResultState.Failure)
            {
                score -= 20;
            }
            else if (State == ResultState.PartialSuccess)
            {
                score -= 10;
            }

            return Math.Max(0, Math.Min(100, score));
        }
    }

    /// <summary>
    ///     Gets a human-readable performance summary.
    /// </summary>
    public string Summary =>
        $"{ResultType} completed in {ExecutionTimeMs:F2}ms with {ExceptionCount} exceptions " +
        $"(State: {State}, Score: {PerformanceScore}/100)";

    /// <summary>
    ///     Gets a detailed performance report.
    /// </summary>
    public string DetailedReport =>
        $"""
         Performance Metrics for {ResultType}:
         - Execution Time: {ExecutionTimeMs:F2}ms
         - Exception Count: {ExceptionCount}
         - State: {State}
         - Performance Score: {PerformanceScore}/100
         - Is Performant: {IsPerformant}
         - Is Slow: {IsSlow}
         - Is Very Slow: {IsVerySlow}
         - Captured At: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}
         """;

    /// <summary>
    ///     Creates a read-only frozen dictionary of metrics for structured logging.
    /// </summary>
    /// <returns>
    ///     A read-only frozen dictionary containing all performance metrics suitable for structured logging frameworks.
    /// </returns>
    /// <remarks>
    ///     The dictionary includes execution time, exception count, state, result type, performance score,
    ///     and boolean performance indicators (IsPerformant, IsSlow, IsVerySlow).
    ///     Returns a FrozenDictionary for optimal read performance and reduced memory overhead.
    /// </remarks>
    public IReadOnlyDictionary<string, object> ToDictionary()
    {
        return FrozenDictionary.ToFrozenDictionary(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ExecutionTimeMs"] = ExecutionTimeMs,
                ["ExceptionCount"] = ExceptionCount,
                ["State"] = State.ToString(),
                ["ResultType"] = ResultType,
                ["PerformanceScore"] = PerformanceScore,
                ["IsPerformant"] = IsPerformant,
                ["IsSlow"] = IsSlow,
                ["IsVerySlow"] = IsVerySlow,
                ["Timestamp"] = Timestamp
            },
            StringComparer.Ordinal);
    }

    /// <summary>
    ///     Checks if this result meets the specified performance threshold.
    /// </summary>
    /// <param name="maxExecutionTime">The maximum acceptable execution time.</param>
    /// <param name="maxExceptions">The maximum acceptable number of exceptions (default: 1).</param>
    /// <returns>
    ///     <c>true</c> if the result meets both the execution time and exception count thresholds; otherwise, <c>false</c>.
    /// </returns>
    public bool MeetsThreshold(TimeSpan maxExecutionTime, int maxExceptions = 1) =>
        ExecutionTime <= maxExecutionTime && ExceptionCount <= maxExceptions;
}
