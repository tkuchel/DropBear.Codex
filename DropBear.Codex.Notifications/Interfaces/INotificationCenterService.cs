#region

using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

public interface INotificationCenterService
{
    Task<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount)> GetNotificationsAsync(
        NotificationFilter filter, CancellationToken cancellationToken = default);

    Task<NotificationRecord?> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DismissAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task DismissAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationPreferences> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SavePreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default);

    // Event raising method
    Task RaiseNotificationReceivedEvent(NotificationRecord notification);
    Task RaiseNotificationReadEvent(Guid notificationId);

    Task RaiseNotificationDismissedEvent(Guid notificationId);

    // Events for real-time updates
    event Func<NotificationRecord, Task> OnNotificationReceived;
    event Func<Guid, Task> OnNotificationRead;
    event Func<Guid, Task> OnNotificationDismissed;
}
