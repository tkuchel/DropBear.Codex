#region

using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Notifications.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly DbContext _dbContext;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(
        DbContext dbContext,
        ILogger<NotificationRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<NotificationRecord?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Set<NotificationRecord>()
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount)> GetForUserAsync(
        NotificationFilter filter)
    {
        var query = _dbContext.Set<NotificationRecord>()
            .Where(n => n.UserId == filter.UserId);

        // Apply filters
        if (filter.IsRead.HasValue)
        {
            query = query.Where(n => filter.IsRead.Value ? n.ReadAt != null : n.ReadAt == null);
        }

        if (filter.IsDismissed.HasValue)
        {
            query = query.Where(n => filter.IsDismissed.Value ? n.DismissedAt != null : n.DismissedAt == null);
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(n => n.Type == filter.Type.Value);
        }

        if (filter.Severity.HasValue)
        {
            query = query.Where(n => n.Severity == filter.Severity.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(n => n.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(n => n.CreatedAt <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            query = query.Where(n => n.Message.Contains(filter.SearchText) ||
                                     (n.Title != null && n.Title.Contains(filter.SearchText)));
        }

        // Count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = filter.SortBy.ToLower() switch
        {
            "createdat" => filter.SortDescending
                ? query.OrderByDescending(n => n.CreatedAt)
                : query.OrderBy(n => n.CreatedAt),
            "severity" => filter.SortDescending
                ? query.OrderByDescending(n => n.Severity)
                : query.OrderBy(n => n.Severity),
            "type" => filter.SortDescending
                ? query.OrderByDescending(n => n.Type)
                : query.OrderBy(n => n.Type),
            _ => filter.SortDescending
                ? query.OrderByDescending(n => n.CreatedAt)
                : query.OrderBy(n => n.CreatedAt)
        };

        // Apply pagination
        query = query.Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize);

        var notifications = await query.ToListAsync();

        return (notifications, totalCount);
    }

    public async Task CreateAsync(NotificationRecord notification)
    {
        try
        {
            // Ensure ID is set
            if (notification.Id == Guid.Empty)
            {
                notification.Id = Guid.NewGuid();
            }

            // Ensure CreatedAt is set
            if (notification.CreatedAt == default)
            {
                notification.CreatedAt = DateTime.UtcNow;
            }

            await _dbContext.Set<NotificationRecord>().AddAsync(notification);
            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("Created notification: {Id}", notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification: {Id}", notification.Id);
            throw;
        }
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        try
        {
            var notification = await GetByIdAsync(id);

            if (notification == null)
            {
                _logger.LogWarning("Cannot mark notification as read. Not found: {Id}", id);
                return;
            }

            // Only update if not already read
            if (notification.ReadAt == null)
            {
                notification.ReadAt = DateTime.UtcNow;
                _dbContext.Update(notification);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Marked notification as read: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read: {Id}", id);
            throw;
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Directly update all unread notifications for the user
            // This is more efficient than loading them all into memory
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.ReadAt, now));

            _logger.LogDebug("Marked {Count} notifications as read for user {UserId}", count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read for user: {UserId}", userId);
            throw;
        }
    }

    public async Task DismissAsync(Guid id)
    {
        try
        {
            var notification = await GetByIdAsync(id);

            if (notification == null)
            {
                _logger.LogWarning("Cannot dismiss notification. Not found: {Id}", id);
                return;
            }

            // Only update if not already dismissed
            if (notification.DismissedAt == null)
            {
                notification.DismissedAt = DateTime.UtcNow;
                _dbContext.Update(notification);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Dismissed notification: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss notification: {Id}", id);
            throw;
        }
    }

    public async Task DismissAllAsync(Guid userId)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Directly update all undismissed notifications for the user
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.DismissedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.DismissedAt, now));

            _logger.LogDebug("Dismissed {Count} notifications for user {UserId}", count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss all notifications for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        try
        {
            return await _dbContext.Set<NotificationRecord>()
                .CountAsync(n => n.UserId == userId && n.ReadAt == null && n.DismissedAt == null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread notification count for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<NotificationPreferences?> GetUserPreferencesAsync(Guid userId)
    {
        try
        {
            return await _dbContext.Set<NotificationPreferences>()
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification preferences for user: {UserId}", userId);
            throw;
        }
    }

    public async Task SaveUserPreferencesAsync(NotificationPreferences preferences)
    {
        try
        {
            var existingPreferences = await _dbContext.Set<NotificationPreferences>()
                .FirstOrDefaultAsync(p => p.UserId == preferences.UserId);

            if (existingPreferences == null)
            {
                await _dbContext.Set<NotificationPreferences>().AddAsync(preferences);
            }
            else
            {
                existingPreferences.EnableToastNotifications = preferences.EnableToastNotifications;
                existingPreferences.EnableInboxNotifications = preferences.EnableInboxNotifications;
                existingPreferences.EnableEmailNotifications = preferences.EnableEmailNotifications;
                existingPreferences.SerializedTypePreferences = preferences.SerializedTypePreferences;
                _dbContext.Update(existingPreferences);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("Saved notification preferences for user: {UserId}", preferences.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification preferences for user: {UserId}", preferences.UserId);
            throw;
        }
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetRecentNotificationsAsync(
        Guid userId,
        int count = 5,
        bool unreadOnly = true)
    {
        try
        {
            var query = _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.DismissedAt == null);

            if (unreadOnly)
            {
                query = query.Where(n => n.ReadAt == null);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent notifications for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> DeleteOldNotificationsAsync(int daysToKeep = 90)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            // Delete notifications older than the cutoff date
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.CreatedAt < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Deleted {Count} old notifications (older than {DaysToKeep} days)",
                count, daysToKeep);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old notifications");
            return false;
        }
    }
}
