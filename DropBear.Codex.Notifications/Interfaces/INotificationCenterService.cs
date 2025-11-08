#region

using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

/// <summary>
///     Service interface for managing notifications and notification preferences.
///     All methods return Results to enable proper error handling without exceptions.
/// </summary>
public interface INotificationCenterService
{
    /// <summary>
    ///     Retrieves notifications based on filter criteria.
    /// </summary>
    Task<Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>> GetNotificationsAsync(
        NotificationFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a specific notification by ID.
    /// </summary>
    Task<Result<NotificationRecord?, NotificationError>> GetNotificationAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a notification as read.
    /// </summary>
    Task<Result<Unit, NotificationError>> MarkAsReadAsync(
        Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks all notifications for a user as read.
    /// </summary>
    Task<Result<Unit, NotificationError>> MarkAllAsReadAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dismisses a notification.
    /// </summary>
    Task<Result<Unit, NotificationError>> DismissAsync(
        Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dismisses all notifications for a user.
    /// </summary>
    Task<Result<Unit, NotificationError>> DismissAllAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the count of unread notifications for a user.
    /// </summary>
    Task<Result<int, NotificationError>> GetUnreadCountAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves notification preferences for a user.
    /// </summary>
    Task<Result<NotificationPreferences?, NotificationError>> GetPreferencesAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves notification preferences for a user.
    /// </summary>
    Task<Result<Unit, NotificationError>> SavePreferencesAsync(
        NotificationPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Raises the notification received event for subscribers.
    /// </summary>
    /// <param name="notification">The notification that was received.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RaiseNotificationReceivedEvent(NotificationRecord notification);

    /// <summary>
    ///     Raises the notification read event for subscribers.
    /// </summary>
    /// <param name="notificationId">The ID of the notification that was read.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RaiseNotificationReadEvent(Guid notificationId);

    /// <summary>
    ///     Raises the notification dismissed event for subscribers.
    /// </summary>
    /// <param name="notificationId">The ID of the notification that was dismissed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RaiseNotificationDismissedEvent(Guid notificationId);

    /// <summary>
    ///     Event fired when a notification is received. Subscribers receive the notification record.
    /// </summary>
#pragma warning disable MA0046 // Async event handlers are intentional for UI integration
    event Func<NotificationRecord, Task> OnNotificationReceived;

    /// <summary>
    ///     Event fired when a notification is marked as read. Subscribers receive the notification ID.
    /// </summary>
    event Func<Guid, Task> OnNotificationRead;

    /// <summary>
    ///     Event fired when a notification is dismissed. Subscribers receive the notification ID.
    /// </summary>
    event Func<Guid, Task> OnNotificationDismissed;
#pragma warning restore MA0046
}
