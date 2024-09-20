namespace DropBear.Codex.Notifications.Exceptions;

/// <summary>
///     Represents errors that occur during notification persistence operations.
/// </summary>
public class NotificationPersistenceException : Exception
{
    public NotificationPersistenceException(string message) : base(message) { }

    public NotificationPersistenceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
