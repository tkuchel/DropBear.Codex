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

    // Event raising methods
    Task RaiseNotificationReceivedEvent(NotificationRecord notification);
    Task RaiseNotificationReadEvent(Guid notificationId);
    Task RaiseNotificationDismissedEvent(Guid notificationId);

    // Events for real-time updates
    event Func<NotificationRecord, Task> OnNotificationReceived;
    event Func<Guid, Task> OnNotificationRead;
    event Func<Guid, Task> OnNotificationDismissed;
}
