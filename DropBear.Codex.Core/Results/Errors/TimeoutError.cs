#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur when an operation times out
/// </summary>
public record TimeoutError : ResultError
{
    public TimeoutError(TimeSpan duration)
        : base(FormatErrorMessage(duration))
    {
        Duration = duration;
        OccurredAt = DateTime.UtcNow;
    }

    public TimeoutError(TimeSpan duration, string operation)
        : base(FormatErrorMessage(duration, operation))
    {
        Duration = duration;
        Operation = operation;
        OccurredAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the duration after which the timeout occurred
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    ///     Gets the name of the operation that timed out (if specified)
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    ///     Gets the UTC timestamp when the timeout occurred
    /// </summary>
    public DateTime OccurredAt { get; }

    private static string FormatErrorMessage(TimeSpan duration)
    {
        return $"Operation timed out after {FormatDuration(duration)}";
    }

    private static string FormatErrorMessage(TimeSpan duration, string operation)
    {
        return $"Operation '{operation}' timed out after {FormatDuration(duration)}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
        {
            return $"{duration.TotalMilliseconds:F0}ms";
        }

        if (duration.TotalSeconds < 60)
        {
            return $"{duration.TotalSeconds:F1}s";
        }

        return $"{duration.TotalMinutes:F1}m";
    }
}
