#region

using System.Text.Json;
using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Errors;
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

    public async Task<Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>> GetNotificationsAsync(
        NotificationFilter filter, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _repository.GetForUserAsync(filter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notifications for user {UserId}", filter.UserId);
            return Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<NotificationRecord?, NotificationError>> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification {Id}", id);
            return Result<NotificationRecord?, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<Unit, NotificationError>> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _repository.MarkAsReadAsync(notificationId, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            if (OnNotificationRead != null)
            {
                await RaiseNotificationReadEvent(notificationId).ConfigureAwait(false);
            }

            _logger.LogDebug("Notification {Id} marked as read", notificationId);
            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification {Id} as read", notificationId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<Unit, NotificationError>> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _repository.MarkAllAsReadAsync(userId, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            // Since we don't know which notifications were affected, we can't trigger individual events
            // We could consider adding a "batch" event for this scenario if needed

            _logger.LogDebug("All notifications for user {UserId} marked as read", userId);
            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications as read for user {UserId}", userId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<Unit, NotificationError>> DismissAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _repository.DismissAsync(notificationId, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            if (OnNotificationDismissed != null)
            {
                await RaiseNotificationDismissedEvent(notificationId).ConfigureAwait(false);
            }

            _logger.LogDebug("Notification {Id} dismissed", notificationId);
            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss notification {Id}", notificationId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<Unit, NotificationError>> DismissAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _repository.DismissAllAsync(userId, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            // Similar to MarkAllAsRead, we don't have individual IDs to trigger events

            _logger.LogDebug("All notifications for user {UserId} dismissed", userId);
            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss all notifications for user {UserId}", userId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<int, NotificationError>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return await _repository.GetUnreadCountAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread count for user {UserId}", userId);
            return Result<int, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
    }

    public async Task<Result<NotificationPreferences?, NotificationError>> GetPreferencesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dbPreferencesResult = await _repository.GetUserPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);

            if (!dbPreferencesResult.IsSuccess)
            {
                return Result<NotificationPreferences?, NotificationError>.Failure(dbPreferencesResult.Error);
            }

            var dbPreferences = dbPreferencesResult.Value;

            if (dbPreferences == null)
            {
                // Return default preferences if not found
                var defaultPreferences = new NotificationPreferences
                {
                    UserId = userId,
                    EnableToastNotifications = true,
                    EnableInboxNotifications = true,
                    EnableEmailNotifications = false,
                    TypePreferences = new Dictionary<string, NotificationTypePreference>()
                };
                return Result<NotificationPreferences?, NotificationError>.Success(defaultPreferences);
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

            return Result<NotificationPreferences?, NotificationError>.Success(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification preferences for user {UserId}", userId);
            return Result<NotificationPreferences?, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<Result<Unit, NotificationError>> SavePreferencesAsync(NotificationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
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

            var result = await _repository.SaveUserPreferencesAsync(dbPreferences, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            _logger.LogDebug("Saved notification preferences for user {UserId}", preferences.UserId);
            return Result<Unit, NotificationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification preferences for user {UserId}", preferences.UserId);
            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
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

    public async Task<Result<IReadOnlyList<NotificationRecord>, NotificationError>> GetRecentNotificationsAsync(
        Guid userId,
        int count = 5,
        bool unreadOnly = true,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return await _repository.GetRecentNotificationsAsync(userId, count, unreadOnly, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent notifications for user {UserId}", userId);
            return Result<IReadOnlyList<NotificationRecord>, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
        }
    }

    public async Task<Result<bool, NotificationError>> PurgeOldNotificationsAsync(
        int daysToKeep = 90,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _repository.DeleteOldNotificationsAsync(daysToKeep, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return result;
            }

            if (result.Value)
            {
                _logger.LogInformation("Successfully purged old notifications (older than {Days} days)", daysToKeep);
            }
            else
            {
                _logger.LogDebug("No old notifications to purge (older than {Days} days)", daysToKeep);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during notification purge operation");
            return Result<bool, NotificationError>.Failure(
                NotificationError.FromException(ex),
                ex);
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
