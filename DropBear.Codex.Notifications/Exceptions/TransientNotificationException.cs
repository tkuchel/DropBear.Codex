namespace DropBear.Codex.Notifications.Exceptions;

/// <summary>
///     Exception representing a transient notification error.
/// </summary>
public class TransientNotificationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TransientNotificationException" /> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public TransientNotificationException(string message)
        : base(message)
    {
    }
}
