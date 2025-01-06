#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents an error that occurs when an operation times out.
/// </summary>
public sealed record TimeoutError : ResultError
{
    private static readonly (string Unit, int Scale)[] TimeFormats =
    [
        ("ms", 1), // Milliseconds
        ("s", 1000), // Seconds
        ("m", 60), // Minutes
        ("h", 60) // Hours
    ];

    #region Constructors

    /// <summary>
    ///     Creates a new <see cref="TimeoutError" /> with the specified <paramref name="duration" /> and operation name.
    /// </summary>
    /// <param name="duration">The time span after which the timeout occurred.</param>
    /// <param name="operation">Optional name of the operation that timed out.</param>
    public TimeoutError(TimeSpan duration, string? operation = null)
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
    ///     Gets the duration after which the timeout occurred.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    ///     Gets the name of the operation that timed out, if provided.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the timeout was recorded.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    ///     Gets how long it has been since the timeout occurred.
    /// </summary>
    public TimeSpan ElapsedSinceTimeout => DateTime.UtcNow - OccurredAt;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Returns a copy of this <see cref="TimeoutError" /> with the <see cref="Operation" /> updated.
    /// </summary>
    /// <param name="operation">The new operation name to set.</param>
    public TimeoutError WithOperation(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return this with { Operation = operation };
    }

    /// <summary>
    ///     Checks if the timeout occurred within the specified <paramref name="duration" />.
    /// </summary>
    /// <param name="duration">A time span to compare against the elapsed time since timeout.</param>
    public bool OccurredWithin(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration.Ticks);
        return ElapsedSinceTimeout <= duration;
    }

    /// <summary>
    ///     Returns a string representation of this timeout error,
    ///     including how long ago the timeout occurred.
    /// </summary>
    public override string ToString()
    {
        var baseMessage = Message;
        var elapsedInfo = $" (occurred {FormatDuration(ElapsedSinceTimeout)} ago)";
        return baseMessage + elapsedInfo;
    }

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
        // Convert TimeSpan to total milliseconds
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
}
