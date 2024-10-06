namespace DropBear.Codex.Notifications.Exceptions;


/// <summary>
/// Exception representing a permanent notification error.
/// </summary>
public class PermanentNotificationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PermanentNotificationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PermanentNotificationException(string message)
        : base(message)
    {
    }
}
