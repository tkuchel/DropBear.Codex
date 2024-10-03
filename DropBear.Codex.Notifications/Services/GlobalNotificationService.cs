#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Helpers;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
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
    private readonly IAsyncPublisher<string, byte[]> _publisher;
    private readonly INotificationSerializationService _serializationService;
    private readonly UserBufferedPublisher<byte[]> _userBufferedPublisher;

    public GlobalNotificationService(
        IAsyncPublisher<string, byte[]> publisher,
        UserBufferedPublisher<byte[]> userBufferedPublisher,
        NotificationBatchService batchService,
        INotificationSerializationService serializationService)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _userBufferedPublisher =
            userBufferedPublisher ?? throw new ArgumentNullException(nameof(userBufferedPublisher));
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
        _serializationService = serializationService ?? throw new ArgumentNullException(nameof(serializationService));
        _logger = LoggerFactory.Logger.ForContext<GlobalNotificationService>();
    }

    /// <summary>
    ///     Publishes a notification to a specific channel.
    /// </summary>
    public async Task<Result> PublishNotificationAsync(string channelId, Notification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ArgumentException("Channel ID cannot be null or empty.", nameof(channelId));
        }

        if (string.IsNullOrEmpty(notification.Message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(notification.Message));
        }

        _logger.Information("Publishing notification for channel {ChannelId}: {Message}", channelId,
            notification.Message);

        try
        {
            var data = await _serializationService.SerializeNotificationAsync(notification, cancellationToken);
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
    ///     Publishes a notification to a specified channel with serialization.
    /// </summary>
    public async Task<Result> PublishNotificationAsync(
        string channelId,
        AlertType alertType,
        string message,
        NotificationSeverity severity = NotificationSeverity.Information,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification(alertType, message, severity);
        return await PublishNotificationAsync(channelId, notification, cancellationToken);
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
            var notification = await _serializationService.DeserializeNotificationAsync(data, cancellationToken);
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
        if (maxRetries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be greater than 0.");
        }

        return await RetryHelper.RetryAsync(
            () => PublishNotificationAsync(channelId, alertType, message, NotificationSeverity.Information,
                cancellationToken),
            maxRetries,
            TimeSpan.FromSeconds(2), // or use exponential backoff
            _logger,
            $"Failed to publish notification on attempt {{Attempt}} for channel {channelId}",
            cancellationToken
        );
    }

    /// <summary>
    ///     Publishes and persists critical notifications.
    /// </summary>
    public async Task<Result> PublishAndPersistCriticalNotificationAsync(
        string channelId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var result =
            await PublishNotificationWithRetryAsync(channelId, AlertType.SystemAlert, message, 5, cancellationToken);

        if (!result.IsSuccess)
        {
            var notification = new Notification(AlertType.SystemAlert, message, NotificationSeverity.Critical);
            await _serializationService.PersistNotificationAsync(notification, cancellationToken);
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
        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for channel {ChannelId}: {Message}", channelId, message);

        var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
        var notification = new Notification(AlertType.TaskProgress, message, NotificationSeverity.Information,
            additionalData);

        try
        {
            var data = await _serializationService.SerializeNotificationAsync(notification, cancellationToken);
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
        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for channel {ChannelId}: {Message}", channelId, message);

        var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
        var notification = new Notification(AlertType.TaskProgress, message, NotificationSeverity.Information,
            additionalData);

        try
        {
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

    private async Task PublishToChannelsAsync(string channelId, byte[] data, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(channelId, data, cancellationToken);
        await _userBufferedPublisher.PublishAsync(channelId, data);
    }
}
