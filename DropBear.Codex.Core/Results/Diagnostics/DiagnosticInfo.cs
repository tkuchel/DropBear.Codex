#region

using System.Diagnostics;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Contains diagnostic information about a result operation.
///     Immutable record struct optimized for .NET 9 with minimal allocations.
/// </summary>
[DebuggerDisplay("State = {State}, Type = {ResultType.Name}, Age = {Age.TotalMilliseconds}ms")]
public readonly record struct DiagnosticInfo
{
    /// <summary>
    ///     Initializes a new instance of DiagnosticInfo.
    /// </summary>
    public DiagnosticInfo(
        ResultState state,
        Type resultType,
        DateTime createdAt,
        string? traceId)
    {
        State = state;
        ResultType = resultType;
        CreatedAt = createdAt;
        TraceId = traceId;
        ActivityId = Activity.Current?.Id;
        ParentActivityId = Activity.Current?.ParentId;
    }

    /// <summary>
    ///     Gets the result state at the time of creation.
    /// </summary>
    public ResultState State { get; init; }

    /// <summary>
    ///     Gets the type of the result.
    /// </summary>
    public Type ResultType { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the result was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    ///     Gets the trace ID for correlation (if available).
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    ///     Gets the Activity ID for distributed tracing.
    /// </summary>
    public string? ActivityId { get; init; }

    /// <summary>
    ///     Gets the parent Activity ID for distributed tracing.
    /// </summary>
    public string? ParentActivityId { get; init; }

    /// <summary>
    ///     Gets the age of this diagnostic info (time since creation).
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CreatedAt;

    /// <summary>
    ///     Gets a value indicating whether this diagnostic has tracing information.
    /// </summary>
    public bool HasTracing => !string.IsNullOrEmpty(TraceId) || !string.IsNullOrEmpty(ActivityId);

    /// <summary>
    ///     Creates diagnostic info with the current activity context.
    /// </summary>
    public static DiagnosticInfo Create(ResultState state, Type resultType)
    {
        return new DiagnosticInfo(
            state,
            resultType,
            DateTime.UtcNow,
            Activity.Current?.Id);
    }

    /// <summary>
    ///     Gets a formatted string representation for logging.
    ///     Uses modern string interpolation handler for performance.
    /// </summary>
    public string ToLogString()
    {
        return $"[{State}] {ResultType.Name} (Age: {Age.TotalMilliseconds:F2}ms, TraceId: {TraceId ?? "None"})";
    }

    /// <summary>
    ///     Creates a dictionary of diagnostic properties for structured logging.
    ///     Uses collection expressions for modern syntax.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["State"] = State.ToString(),
            ["ResultType"] = ResultType.Name,
            ["CreatedAt"] = CreatedAt,
            ["Age"] = Age.TotalMilliseconds
        };

        if (TraceId is not null)
            dict["TraceId"] = TraceId;

        if (ActivityId is not null)
            dict["ActivityId"] = ActivityId;

        if (ParentActivityId is not null)
            dict["ParentActivityId"] = ParentActivityId;

        return dict;
    }
}
