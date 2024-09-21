#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Serialization.Interfaces;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides global notification services including publishing, persisting, and retrieving notifications.
/// </summary>
public sealed class GlobalNotificationService
{
    private const string GlobalChannelId = "global";
    private readonly NotificationBatchService _batchService;
    private readonly ILogger _logger;
    private readonly NotificationPersistenceService _persistenceService;
    private readonly IAsyncPublisher<string, byte[]> _publisher;
    private readonly ISerializer? _serializer;
    private readonly UserBufferedPublisher<byte[]> _userBufferedPublisher;

    public GlobalNotificationService(
        IAsyncPublisher<string, byte[]> publisher,
        UserBufferedPublisher<byte[]> userBufferedPublisher,
        NotificationPersistenceService persistenceService,
        NotificationBatchService batchService,
        ISerializer? serializer = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _userBufferedPublisher =
            userBufferedPublisher ?? throw new ArgumentNullException(nameof(userBufferedPublisher));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
        _serializer = serializer;
        _logger = LoggerFactory.Logger.ForContext<GlobalNotificationService>();
    }

    /// <summary>
    ///     Publishes a notification for a specific user with optional serialization.
    /// </summary>
    public async Task<Result> PublishNotificationAsync(
        string channelId,
        AlertType alertType,
        string message,
        NotificationSeverity severity = NotificationSeverity.Information,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        var notification = new Notification(alertType, message, severity);
        _logger.Information("Publishing notification for channel {ChannelId}: {Message}", channelId, message);

        try
        {
            var data = await SerializeNotificationAsync(notification, cancellationToken);
            await PublishToChannelsAsync(channelId, data, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing notification for channel {ChannelId}", channelId);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Retrieves the latest buffered notification for a specific channel.
    /// </summary>
    public async Task<Result<Notification?>> GetLatestNotificationForChannelAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        var data = _userBufferedPublisher.GetBufferedMessage(channelId);
        if (data == null)
        {
            return Result<Notification?>.Success(null);
        }

        try
        {
            if (_serializer == null)
            {
                throw new InvalidOperationException("Serializer is not available.");
            }

            var notification = await _serializer.DeserializeAsync<Notification>(data, cancellationToken);
            return Result<Notification?>.Success(notification);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving latest notification for channel {ChannelId}", channelId);
            return Result<Notification?>.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Publishes notifications with retry logic.
    /// </summary>
    public async Task<Result> PublishNotificationWithRetryAsync(
        string channelId,
        AlertType alertType,
        string message,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        if (maxRetries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be greater than 0.");
        }

        var notification = new Notification(alertType, message, NotificationSeverity.Information);
        _logger.Information("Attempting to publish notification for channel {ChannelId}: {Message}", channelId,
            message);

        var data = await SerializeNotificationAsync(notification, cancellationToken);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await PublishToChannelsAsync(channelId, data, cancellationToken);
                _logger.Information("Notification published successfully on attempt {Attempt} for channel {ChannelId}",
                    attempt, channelId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to publish notification on attempt {Attempt} for channel {ChannelId}",
                    attempt,
                    channelId);
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }

        _logger.Error("Failed to publish notification after {MaxRetries} attempts for channel {ChannelId}", maxRetries,
            channelId);
        return Result.Failure($"Failed to publish notification after {maxRetries} attempts.");
    }

    /// <summary>
    ///     Publishes and persists critical notifications.
    /// </summary>
    public async Task<Result> PublishAndPersistCriticalNotificationAsync(
        string channelId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        var result = await PublishNotificationWithRetryAsync(channelId, AlertType.SystemAlert, message, 5,
            cancellationToken);

        if (!result.IsSuccess)
        {
            var notification = new Notification(AlertType.SystemAlert, message, NotificationSeverity.Critical);
            await PersistNotificationAsync(notification, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     Publishes a global notification.
    /// </summary>
    public Task<Result> PublishGlobalNotificationAsync(
        AlertType alertType,
        string message,
        NotificationSeverity severity = NotificationSeverity.Information,
        CancellationToken cancellationToken = default)
    {
        return PublishNotificationAsync(GlobalChannelId, alertType, message, severity, cancellationToken);
    }

    /// <summary>
    ///     Publishes task progress updates.
    /// </summary>
    public async Task<Result> PublishTaskProgressAsync(
        string channelId,
        string taskName,
        int progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(taskName))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
        }

        if (progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
        }

        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for channel {ChannelId}: {Message}", channelId, message);

        try
        {
            var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
            var notification =
                new Notification(AlertType.TaskProgress, message, NotificationSeverity.Information, additionalData);

            var data = await SerializeNotificationAsync(notification, cancellationToken);
            await PublishToChannelsAsync(channelId, data, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing task progress for channel {ChannelId}", channelId);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Publishes task progress updates with batching support.
    /// </summary>
    public async Task<Result> PublishTaskProgressBatchAsync(
        string channelId,
        string taskName,
        int progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(taskName))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
        }

        if (progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
        }

        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for channel {ChannelId}: {Message}", channelId, message);

        try
        {
            var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
            var notification =
                new Notification(AlertType.TaskProgress, message, NotificationSeverity.Information, additionalData);

            await _batchService.AddNotificationToBatchAsync(notification, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing task progress for channel {ChannelId}", channelId);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Publishes a batch of notifications.
    /// </summary>
    public async Task<Result> PublishBatchNotificationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _batchService.PublishBatchAsync(cancellationToken);
            _logger.Information("Batch of notifications published successfully.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing batch of notifications.");
            return Result.Failure(ex.Message, ex);
        }
    }

    private async Task<byte[]> SerializeNotificationAsync(Notification notification,
        CancellationToken cancellationToken)
    {
        if (_serializer == null)
        {
            throw new InvalidOperationException("Serializer is not available.");
        }

        return await _serializer.SerializeAsync(notification, cancellationToken);
    }

    private async Task PublishToChannelsAsync(string userId, byte[] data, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(userId, data, cancellationToken);
        await _userBufferedPublisher.PublishAsync(userId, data);
    }

    private async Task PersistNotificationAsync(Notification notification,
        CancellationToken cancellationToken)
    {
        if (_serializer != null)
        {
            var data = await _serializer.SerializeAsync(notification, cancellationToken);
            await _persistenceService.SaveSerializedNotificationAsync(data);
        }
        else
        {
            await _persistenceService.SaveNotificationAsync(notification);
        }
    }
}
