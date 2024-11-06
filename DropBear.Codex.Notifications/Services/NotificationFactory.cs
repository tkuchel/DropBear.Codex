#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Notifications.Services;

#endregion

public class NotificationFactory : INotificationFactory
{
    /// <summary>
    ///     Creates a new notification with the specified parameters.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="severity">The notification severity.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The notification title.</param>
    /// <param name="data">Additional data for the notification.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification> CreateNotification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        if (channelId == Guid.Empty)
        {
            return Result<Notification>.Failure("ChannelId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return Result<Notification>.Failure("Message cannot be null or whitespace.");
        }

        if (type == NotificationType.NotSpecified)
        {
            return Result<Notification>.Failure("NotificationType must be specified.");
        }

        if (severity == NotificationSeverity.NotSpecified)
        {
            return Result<Notification>.Failure("NotificationSeverity must be specified.");
        }

        var notification = new Notification(channelId, type, severity, message, title, data);
        return Result<Notification>.Success(notification);
    }
}
