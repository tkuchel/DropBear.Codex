#region

using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Errors;
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

    public async Task<Result<NotificationRecord?, NotificationError>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await _dbContext.Set<NotificationRecord>()
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<NotificationRecord?, NotificationError>.Success(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve notification by ID: {Id}", id);
            return Result<NotificationRecord?, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("GetByIdAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>> GetForUserAsync(
        NotificationFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
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
            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

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

            var notifications = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

            return Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>
                .Success((notifications, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve notifications for user: {UserId}", filter.UserId);
            return Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("GetForUserAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> CreateAsync(
        NotificationRecord notification,
        CancellationToken cancellationToken = default)
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

            await _dbContext.Set<NotificationRecord>().AddAsync(notification, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Created notification: {Id}", notification.Id);

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification: {Id}", notification.Id);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("CreateAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> MarkAsReadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationResult = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

            if (!notificationResult.IsSuccess)
            {
                return Result<Unit, NotificationError>.Failure(notificationResult.Error!);
            }

            var notification = notificationResult.Value;

            if (notification == null)
            {
                _logger.LogWarning("Cannot mark notification as read. Not found: {Id}", id);
                return Result<Unit, NotificationError>.Failure(NotificationError.NotFound(id));
            }

            // Only update if not already read
            if (notification.ReadAt == null)
            {
                notification.ReadAt = DateTime.UtcNow;
                _dbContext.Update(notification);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Marked notification as read: {Id}", id);
            }

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification as read: {Id}", id);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("MarkAsReadAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Directly update all unread notifications for the user
            // This is more efficient than loading them all into memory
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.ReadAt, now), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Marked {Count} notifications as read for user {UserId}", count, userId);

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read for user: {UserId}", userId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("MarkAllAsReadAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> DismissAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationResult = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

            if (!notificationResult.IsSuccess)
            {
                return Result<Unit, NotificationError>.Failure(notificationResult.Error!);
            }

            var notification = notificationResult.Value;

            if (notification == null)
            {
                _logger.LogWarning("Cannot dismiss notification. Not found: {Id}", id);
                return Result<Unit, NotificationError>.Failure(NotificationError.NotFound(id));
            }

            // Only update if not already dismissed
            if (notification.DismissedAt == null)
            {
                notification.DismissedAt = DateTime.UtcNow;
                _dbContext.Update(notification);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Dismissed notification: {Id}", id);
            }

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss notification: {Id}", id);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("DismissAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> DismissAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Directly update all undismissed notifications for the user
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.DismissedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.DismissedAt, now), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Dismissed {Count} notifications for user {UserId}", count, userId);

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss all notifications for user: {UserId}", userId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("DismissAllAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<int, NotificationError>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _dbContext.Set<NotificationRecord>()
                .CountAsync(n => n.UserId == userId && n.ReadAt == null && n.DismissedAt == null, cancellationToken)
                .ConfigureAwait(false);

            return Result<int, NotificationError>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread notification count for user: {UserId}", userId);
            return Result<int, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("GetUnreadCountAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<NotificationPreferences?, NotificationError>> GetUserPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preferences = await _dbContext.Set<NotificationPreferences>()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            return Result<NotificationPreferences?, NotificationError>.Success(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification preferences for user: {UserId}", userId);
            return Result<NotificationPreferences?, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("GetUserPreferencesAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<Unit, NotificationError>> SaveUserPreferencesAsync(
        NotificationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existingPreferences = await _dbContext.Set<NotificationPreferences>()
                .FirstOrDefaultAsync(p => p.UserId == preferences.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (existingPreferences == null)
            {
                await _dbContext.Set<NotificationPreferences>().AddAsync(preferences, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                existingPreferences.EnableToastNotifications = preferences.EnableToastNotifications;
                existingPreferences.EnableInboxNotifications = preferences.EnableInboxNotifications;
                existingPreferences.EnableEmailNotifications = preferences.EnableEmailNotifications;
                existingPreferences.SerializedTypePreferences = preferences.SerializedTypePreferences;
                _dbContext.Update(existingPreferences);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Saved notification preferences for user: {UserId}", preferences.UserId);

            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification preferences for user: {UserId}", preferences.UserId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("SaveUserPreferencesAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<IReadOnlyList<NotificationRecord>, NotificationError>> GetRecentNotificationsAsync(
        Guid userId,
        int count = 5,
        bool unreadOnly = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.Set<NotificationRecord>()
                .Where(n => n.UserId == userId && n.DismissedAt == null);

            if (unreadOnly)
            {
                query = query.Where(n => n.ReadAt == null);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<IReadOnlyList<NotificationRecord>, NotificationError>.Success(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent notifications for user: {UserId}", userId);
            return Result<IReadOnlyList<NotificationRecord>, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("GetRecentNotificationsAsync", ex.Message),
                ex);
        }
    }

    public async Task<Result<bool, NotificationError>> DeleteOldNotificationsAsync(
        int daysToKeep = 90,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            // Delete notifications older than the cutoff date
            var count = await _dbContext.Set<NotificationRecord>()
                .Where(n => n.CreatedAt < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Deleted {Count} old notifications (older than {DaysToKeep} days)",
                count, daysToKeep);

            return Result<bool, NotificationError>.Success(count > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old notifications");
            return Result<bool, NotificationError>.Failure(
                NotificationError.DatabaseOperationFailed("DeleteOldNotificationsAsync", ex.Message),
                ex);
        }
    }
}
