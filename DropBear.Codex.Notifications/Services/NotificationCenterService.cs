#region

using System.Text.Json;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Notifications.Services;

public class NotificationCenterService : INotificationCenterService, IDisposable
{
    private readonly ILogger<NotificationCenterService> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly INotificationRepository _repository;
    private bool _disposed;

    public NotificationCenterService(
        INotificationRepository repository,
        ILogger<NotificationCenterService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _operationLock.Dispose();
        _disposed = true;

        _logger.LogDebug("NotificationCenterService disposed");
    }

    public event Func<NotificationRecord, Task>? OnNotificationReceived;
    public event Func<Guid, Task>? OnNotificationRead;
    public event Func<Guid, Task>? OnNotificationDismissed;

    public async Task<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount)> GetNotificationsAsync(
        NotificationFilter filter, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await _repository.GetForUserAsync(filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notifications for user {UserId}", filter.UserId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<NotificationRecord?> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await _repository.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification {Id}", id);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.MarkAsReadAsync(notificationId);

            if (OnNotificationRead != null)
            {
                await RaiseNotificationReadEvent(notificationId);
            }

            _logger.LogDebug("Notification {Id} marked as read", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification {Id} as read", notificationId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.MarkAllAsReadAsync(userId);

            // Since we don't know which notifications were affected, we can't trigger individual events
            // We could consider adding a "batch" event for this scenario if needed

            _logger.LogDebug("All notifications for user {UserId} marked as read", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read for user {UserId}", userId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task DismissAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.DismissAsync(notificationId);

            if (OnNotificationDismissed != null)
            {
                await RaiseNotificationDismissedEvent(notificationId);
            }

            _logger.LogDebug("Notification {Id} dismissed", notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss notification {Id}", notificationId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task DismissAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await _repository.DismissAllAsync(userId);

            // Similar to MarkAllAsRead, we don't have individual IDs to trigger events

            _logger.LogDebug("All notifications for user {UserId} dismissed", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss all notifications for user {UserId}", userId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return await _repository.GetUnreadCountAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread count for user {UserId}", userId);
            throw;
        }
    }

    public async Task<NotificationPreferences> GetPreferencesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var dbPreferences = await _repository.GetUserPreferencesAsync(userId);

            if (dbPreferences == null)
            {
                // Return default preferences if not found
                return new NotificationPreferences
                {
                    UserId = userId,
                    EnableToastNotifications = true,
                    EnableInboxNotifications = true,
                    EnableEmailNotifications = false,
                    TypePreferences = new Dictionary<string, NotificationTypePreference>()
                };
            }

            // Convert from DB model to service model
            var preferences = new NotificationPreferences
            {
                UserId = dbPreferences.UserId,
                EnableToastNotifications = dbPreferences.EnableToastNotifications,
                EnableInboxNotifications = dbPreferences.EnableInboxNotifications,
                EnableEmailNotifications = dbPreferences.EnableEmailNotifications
            };

            // Deserialize type preferences if available
            if (!string.IsNullOrEmpty(dbPreferences.SerializedTypePreferences))
            {
                try
                {
                    preferences.TypePreferences =
                        JsonSerializer.Deserialize<Dictionary<string, NotificationTypePreference>>(
                            dbPreferences.SerializedTypePreferences,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new Dictionary<string, NotificationTypePreference>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize type preferences for user {UserId}", userId);
                    preferences.TypePreferences = new Dictionary<string, NotificationTypePreference>();
                }
            }

            return preferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification preferences for user {UserId}", userId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SavePreferencesAsync(NotificationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            // Convert from service model to DB model
            var dbPreferences = new NotificationPreferences
            {
                UserId = preferences.UserId,
                EnableToastNotifications = preferences.EnableToastNotifications,
                EnableInboxNotifications = preferences.EnableInboxNotifications,
                EnableEmailNotifications = preferences.EnableEmailNotifications
            };

            // Serialize type preferences if available
            if (preferences.TypePreferences.Count > 0)
            {
                dbPreferences.SerializedTypePreferences = JsonSerializer.Serialize(
                    preferences.TypePreferences,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }

            await _repository.SaveUserPreferencesAsync(dbPreferences);

            _logger.LogDebug("Saved notification preferences for user {UserId}", preferences.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification preferences for user {UserId}", preferences.UserId);
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RaiseNotificationReceivedEvent(NotificationRecord notification)
    {
        ThrowIfDisposed();

        var handlers = OnNotificationReceived;
        if (handlers != null)
        {
            try
            {
                await handlers(notification);
                _logger.LogDebug("NotificationReceived event raised for notification {Id}", notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising NotificationReceived event for {Id}", notification.Id);
            }
        }
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetRecentNotificationsAsync(
        Guid userId,
        int count = 5,
        bool unreadOnly = true,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return await _repository.GetRecentNotificationsAsync(userId, count, unreadOnly);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> PurgeOldNotificationsAsync(
        int daysToKeep = 90,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var result = await _repository.DeleteOldNotificationsAsync(daysToKeep);

            if (result)
            {
                _logger.LogInformation("Successfully purged old notifications (older than {Days} days)", daysToKeep);
            }
            else
            {
                _logger.LogWarning("Failed to purge old notifications");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during notification purge operation");
            return false;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RaiseNotificationReadEvent(Guid notificationId)
    {
        var handlers = OnNotificationRead;
        if (handlers != null)
        {
            try
            {
                await handlers(notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising NotificationRead event for {Id}", notificationId);
            }
        }
    }

    public async Task RaiseNotificationDismissedEvent(Guid notificationId)
    {
        var handlers = OnNotificationDismissed;
        if (handlers != null)
        {
            try
            {
                await handlers(notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising NotificationDismissed event for {Id}", notificationId);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NotificationCenterService));
        }
    }
}
