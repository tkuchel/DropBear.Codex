#region

using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

public interface INotificationRepository
{
    Task<NotificationRecord?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount)> GetForUserAsync(NotificationFilter filter);
    Task CreateAsync(NotificationRecord notification);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync(Guid userId);
    Task DismissAsync(Guid id);
    Task DismissAllAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<NotificationPreferences?> GetUserPreferencesAsync(Guid userId);
    Task SaveUserPreferencesAsync(NotificationPreferences preferences);
    Task<IReadOnlyList<NotificationRecord>> GetRecentNotificationsAsync(Guid userId, int count = 5, bool unreadOnly = true);
    Task<bool> DeleteOldNotificationsAsync(int daysToKeep = 90);
}
