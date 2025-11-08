#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

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
    Result<Notification, NotificationError> CreateNotification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null);

    /// <summary>
    ///     Creates a new information notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    Result<Notification, NotificationError> CreateInfoNotification(
        Guid channelId,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null);

    /// <summary>
    ///     Creates a new success notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    Result<Notification, NotificationError> CreateSuccessNotification(
        Guid channelId,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null);

    /// <summary>
    ///     Creates a new warning notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    Result<Notification, NotificationError> CreateWarningNotification(
        Guid channelId,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null);

    /// <summary>
    ///     Creates a new error notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    Result<Notification, NotificationError> CreateErrorNotification(
        Guid channelId,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null);
}
