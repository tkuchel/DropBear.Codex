#region

using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Defines methods for creating notifications.
/// </summary>
public interface INotificationFactory
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
    Result<Notification> CreateNotification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null);
}
