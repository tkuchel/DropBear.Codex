#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur when an operation times out
/// </summary>
public sealed record TimeoutError : ResultError
{
    private static readonly (string Unit, int Scale)[] TimeFormats = { ("ms", 1), ("s", 1000), ("m", 60), ("h", 60) };

    #region Constructors

    /// <summary>
    ///     Creates a new TimeoutError with the specified duration
    /// </summary>
    /// <param name="duration">The duration after which the timeout occurred</param>
    public TimeoutError(TimeSpan duration)
        : this(duration, null)
    {
    }

    /// <summary>
    ///     Creates a new TimeoutError with the specified duration and operation name
    /// </summary>
    /// <param name="duration">The duration after which the timeout occurred</param>
    /// <param name="operation">Optional name of the operation that timed out</param>
    public TimeoutError(TimeSpan duration, string? operation)
        : base(FormatMessage(duration, operation))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration.Ticks);

        Duration = duration;
        Operation = operation;
        OccurredAt = DateTime.UtcNow;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the duration after which the timeout occurred
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    ///     Gets the name of the operation that timed out (if specified)
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the timeout occurred
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    ///     Gets the elapsed time since the timeout occurred
    /// </summary>
    public TimeSpan ElapsedSinceTimeout => DateTime.UtcNow - OccurredAt;

    #endregion

    #region Private Methods

    private static string FormatMessage(TimeSpan duration, string? operation)
    {
        var formattedDuration = FormatDuration(duration);
        return operation is null
            ? $"Operation timed out after {formattedDuration}"
            : $"Operation '{operation}' timed out after {formattedDuration}";
    }

    private static string FormatDuration(TimeSpan timeSpan)
    {
        var value = (double)timeSpan.Ticks / TimeSpan.TicksPerMillisecond;
        var formatIndex = 0;

        while (formatIndex < TimeFormats.Length - 1
               && value >= TimeFormats[formatIndex + 1].Scale)
        {
            value /= TimeFormats[formatIndex + 1].Scale;
            formatIndex++;
        }

        return $"{value:F1}{TimeFormats[formatIndex].Unit}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Creates a copy of this TimeoutError with an updated operation name
    /// </summary>
    public TimeoutError WithOperation(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return this with { Operation = operation };
    }

    /// <summary>
    ///     Checks if the timeout occurred within the specified duration
    /// </summary>
    public bool OccurredWithin(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration.Ticks);
        return ElapsedSinceTimeout <= duration;
    }

    /// <summary>
    ///     Returns a string representation of the error including timing details
    /// </summary>
    public override string ToString()
    {
        var baseMessage = Message;
        var elapsedInfo = $" (occurred {FormatDuration(ElapsedSinceTimeout)} ago)";
        return baseMessage + elapsedInfo;
    }

    #endregion
}
