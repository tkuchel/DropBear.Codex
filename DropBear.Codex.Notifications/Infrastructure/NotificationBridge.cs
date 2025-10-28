#region

using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Notifications.Infrastructure;

public class NotificationBridge : IDisposable
{
    private readonly ILogger<NotificationBridge> _logger;
    private readonly INotificationCenterService _notificationCenterService;
    private readonly INotificationRepository _repository;
    private readonly IAsyncSubscriber<string, Notification> _subscriber;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public NotificationBridge(
        INotificationRepository repository,
        IAsyncSubscriber<string, Notification> subscriber,
        INotificationCenterService notificationCenterService,
        ILogger<NotificationBridge> logger)
    {
        _repository = repository;
        _subscriber = subscriber;
        _notificationCenterService = notificationCenterService;
        _logger = logger;

        // Subscribe to global notifications
        var globalSubscription = _subscriber.Subscribe(
            GlobalConstants.GlobalNotificationChannel,
            HandleNotificationAsync);
        _subscriptions.Add(globalSubscription);

        _logger.LogInformation("NotificationBridge initialized");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        _disposed = true;

        _logger.LogInformation("NotificationBridge disposed");
    }

    public void SubscribeToUserChannel(Guid userId)
    {
        var channelName = $"{GlobalConstants.UserNotificationChannel}.{userId}";
        var userSubscription = _subscriber.Subscribe(
            channelName,
            HandleNotificationAsync);
        _subscriptions.Add(userSubscription);

        _logger.LogDebug("Subscribed to user channel: {ChannelName}", channelName);
    }

    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        try
        {
            _logger.LogDebug("Received notification of type {Type} with severity {Severity}",
                notification.Type, notification.Severity);

            // Don't store task progress notifications
            if (notification.Type == NotificationType.TaskProgress)
            {
                // These are typically ephemeral and don't need to be persisted
                return;
            }

            // Transform notification to record
            var record = new NotificationRecord
            {
                Id = Guid.NewGuid(),
                UserId = notification.ChannelId,
                Type = notification.Type,
                Severity = notification.Severity,
                Message = notification.Message,
                Title = notification.Title,
                CreatedAt = DateTime.UtcNow
            };

            // Add data if present
            if (notification.Data.Count > 0)
            {
                record.SetData(notification.Data.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value,
                    StringComparer.Ordinal));
            }

            // Save to repository
            var createResult = await _repository.CreateAsync(record, token).ConfigureAwait(false);

            if (!createResult.IsSuccess)
            {
                _logger.LogError("Failed to persist notification: {Error}", createResult.Error?.Message ?? "Unknown error");
                return;
            }

            // Notify UI via the notification center service
            await _notificationCenterService.RaiseNotificationReceivedEvent(record).ConfigureAwait(false);

            _logger.LogDebug("Notification persisted with ID: {Id}", record.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notification");
        }
    }
}
