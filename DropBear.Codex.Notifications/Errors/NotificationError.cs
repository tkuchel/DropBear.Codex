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
    public NotificationError(string message)
        : base(message)
    {
        // Default severity is Error
        Severity = NotificationSeverity.Error;
    }

    /// <summary>
    ///     Gets or sets the severity level of this notification error.
    /// </summary>
    public new NotificationSeverity Severity { get; init; }

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
        var error = new NotificationError(ex.Message)
        {
            Severity = isTransient ? NotificationSeverity.Warning : NotificationSeverity.Error,
            IsTransient = isTransient
        };

        // Use WithException to set the exception properly
        var errorWithException = (NotificationError)error.WithException(ex);

        // Add additional metadata
        return (NotificationError)errorWithException
            .WithMetadata("ExceptionType", ex.GetType().Name)
            .WithMetadata("IsTransient", isTransient);
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

    #region Factory Methods

    /// <summary>
    ///     Creates an error for when a notification is not found.
    /// </summary>
    public static NotificationError NotFound(Guid notificationId) =>
        new($"Notification with ID '{notificationId}' was not found.")
        {
            Context = nameof(NotFound),
            Severity = NotificationSeverity.Warning
        };

    /// <summary>
    ///     Creates an error for when notification publishing fails.
    /// </summary>
    public static NotificationError PublishFailed(string reason) =>
        new($"Failed to publish notification: {reason}")
        {
            Context = nameof(PublishFailed),
            Severity = NotificationSeverity.Error,
            IsTransient = true
        };

    /// <summary>
    ///     Creates an error for when notification encryption fails.
    /// </summary>
    public static NotificationError EncryptionFailed(string reason) =>
        new($"Failed to encrypt notification: {reason}")
        {
            Context = nameof(EncryptionFailed),
            Severity = NotificationSeverity.Error
        };

    /// <summary>
    ///     Creates an error for when notification decryption fails.
    /// </summary>
    public static NotificationError DecryptionFailed(string reason) =>
        new($"Failed to decrypt notification: {reason}")
        {
            Context = nameof(DecryptionFailed),
            Severity = NotificationSeverity.Error
        };

    /// <summary>
    ///     Creates an error for when database operations fail.
    /// </summary>
    public static NotificationError DatabaseOperationFailed(string operation, string reason) =>
        new($"Database operation '{operation}' failed: {reason}")
        {
            Context = nameof(DatabaseOperationFailed),
            Severity = NotificationSeverity.Error,
            IsTransient = true
        };

    /// <summary>
    ///     Creates an error for invalid notification data.
    /// </summary>
    public static NotificationError InvalidData(string field, string reason) =>
        new($"Invalid notification data in field '{field}': {reason}")
        {
            Context = nameof(InvalidData),
            Severity = NotificationSeverity.Error
        };

    #endregion
}
