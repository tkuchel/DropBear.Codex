#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Enums;

#endregion

namespace DropBear.Codex.Notifications.Errors;

/// <summary>
///     Represents errors that can occur during notification operations.
///     Provides strongly-typed error information for the Result pattern.
/// </summary>
public sealed record NotificationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the notification failure.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    public NotificationError(string message, DateTime? timestamp = null)
        : base(message, timestamp)
    {
        // Default severity is Error
        Severity = NotificationSeverity.Error;
    }

    /// <summary>
    ///     Gets or sets the severity level of this notification error.
    /// </summary>
    public NotificationSeverity Severity { get; init; }

    /// <summary>
    ///     Gets or sets additional context about where the error occurred.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    ///     Gets or sets a flag indicating whether the error is retryable.
    /// </summary>
    public bool IsTransient { get; init; }

    /// <summary>
    ///     Creates a new <see cref="NotificationError" /> from an exception.
    /// </summary>
    /// <param name="ex">The exception to create the error from.</param>
    /// <param name="isTransient">Whether the error is transient and can be retried.</param>
    /// <returns>A new <see cref="NotificationError" /> instance.</returns>
    public static NotificationError FromException(Exception ex, bool isTransient = false)
    {
        return new NotificationError(ex.Message)
        {
            IsTransient = isTransient,
            Severity = isTransient ? NotificationSeverity.Warning : NotificationSeverity.Error,
            Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ExceptionType"] = ex.GetType().Name, ["StackTrace"] = ex.StackTrace ?? string.Empty
            }
        };
    }

    /// <summary>
    ///     Creates a new <see cref="NotificationError" /> with the specified context.
    /// </summary>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new <see cref="NotificationError" /> with updated context.</returns>
    public NotificationError WithContext(string context)
    {
        return this with { Context = context };
    }

    /// <summary>
    ///     Creates a new <see cref="NotificationError" /> with the specified severity.
    /// </summary>
    /// <param name="severity">The severity of the error.</param>
    /// <returns>A new <see cref="NotificationError" /> with updated severity.</returns>
    public NotificationError WithSeverity(NotificationSeverity severity)
    {
        return this with { Severity = severity };
    }

    /// <summary>
    ///     Creates a new <see cref="NotificationError" /> with the transient flag set.
    /// </summary>
    /// <param name="isTransient">Whether the error is transient.</param>
    /// <returns>A new <see cref="NotificationError" /> with updated transient flag.</returns>
    public NotificationError WithTransient(bool isTransient)
    {
        return this with { IsTransient = isTransient };
    }
}
