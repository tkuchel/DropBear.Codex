#region

using System.Text;
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
    private const string GlobalUserId = "global";
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
        string userId,
        AlertType alertType,
        string message,
        NotificationSeverity severity = NotificationSeverity.Info,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        var notification = new Notification(alertType, message, severity);
        _logger.Information("Publishing notification for user {UserId}: {Message}", userId, message);

        try
        {
            var data = await SerializeNotificationAsync(notification, serialize, cancellationToken);
            await PublishToChannelsAsync(userId, data, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing notification for user {UserId}", userId);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Retrieves the latest buffered notification for a specific user.
    /// </summary>
    public async Task<Result<Notification?>> GetLatestNotificationForUserAsync(
        string userId,
        bool deserialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        var data = _userBufferedPublisher.GetBufferedMessage(userId);
        if (data == null)
        {
            return Result<Notification?>.Success(null);
        }

        try
        {
            var notification = deserialize && _serializer != null
                ? await _serializer.DeserializeAsync<Notification>(data, cancellationToken)
                : new Notification(AlertType.Info, Encoding.UTF8.GetString(data), NotificationSeverity.Info);

            return Result<Notification?>.Success(notification);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving latest notification for user {UserId}", userId);
            return Result<Notification?>.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Publishes notifications with retry logic.
    /// </summary>
    public async Task<Result> PublishNotificationWithRetryAsync(
        string userId,
        AlertType alertType,
        string message,
        int maxRetries = 3,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        if (maxRetries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be greater than 0.");
        }

        var notification = new Notification(alertType, message, NotificationSeverity.Info);
        _logger.Information("Attempting to publish notification for user {UserId}: {Message}", userId, message);

        var data = await SerializeNotificationAsync(notification, serialize, cancellationToken);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await PublishToChannelsAsync(userId, data, cancellationToken);
                _logger.Information("Notification published successfully on attempt {Attempt} for user {UserId}",
                    attempt, userId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to publish notification on attempt {Attempt} for user {UserId}", attempt,
                    userId);
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }

        _logger.Error("Failed to publish notification after {MaxRetries} attempts for user {UserId}", maxRetries,
            userId);
        return Result.Failure($"Failed to publish notification after {maxRetries} attempts.");
    }

    /// <summary>
    ///     Publishes and persists critical notifications.
    /// </summary>
    public async Task<Result> PublishAndPersistCriticalNotificationAsync(
        string userId,
        string message,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        var result = await PublishNotificationWithRetryAsync(userId, AlertType.SystemAlert, message, 5, serialize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            var notification = new Notification(AlertType.SystemAlert, message, NotificationSeverity.Critical);
            await PersistNotificationAsync(notification, serialize, cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     Publishes a global notification.
    /// </summary>
    public Task<Result> PublishGlobalNotificationAsync(
        AlertType alertType,
        string message,
        NotificationSeverity severity = NotificationSeverity.Info,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        return PublishNotificationAsync(GlobalUserId, alertType, message, severity, serialize, cancellationToken);
    }

    /// <summary>
    ///     Publishes task progress updates.
    /// </summary>
    public async Task<Result> PublishTaskProgressAsync(
        string userId,
        string taskName,
        int progress,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        if (string.IsNullOrEmpty(taskName))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
        }

        if (progress < 0 || progress > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
        }

        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for user {UserId}: {Message}", userId, message);

        try
        {
            var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
            var notification =
                new Notification(AlertType.TaskProgress, message, NotificationSeverity.Info, additionalData);

            var data = await SerializeNotificationAsync(notification, serialize, cancellationToken);
            await PublishToChannelsAsync(userId, data, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing task progress for user {UserId}", userId);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Publishes task progress updates with batching support.
    /// </summary>
    public async Task<Result> PublishTaskProgressBatchAsync(
        string userId,
        string taskName,
        int progress,
        bool serialize = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
        }

        if (string.IsNullOrEmpty(taskName))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));
        }

        if (progress < 0 || progress > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
        }

        var message = $"{taskName} is {progress}% complete.";
        _logger.Information("Publishing task progress for user {UserId}: {Message}", userId, message);

        try
        {
            var additionalData = new Dictionary<string, object> { { "TaskName", taskName }, { "Progress", progress } };
            var notification =
                new Notification(AlertType.TaskProgress, message, NotificationSeverity.Info, additionalData);

            await _batchService.AddNotificationToBatchAsync(notification, serialize, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing task progress for user {UserId}", userId);
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

    private async Task<byte[]> SerializeNotificationAsync(Notification notification, bool serialize,
        CancellationToken cancellationToken)
    {
        return serialize && _serializer != null
            ? await _serializer.SerializeAsync(notification, cancellationToken)
            : Encoding.UTF8.GetBytes(notification.Message);
    }

    private async Task PublishToChannelsAsync(string userId, byte[] data, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(userId, data, cancellationToken);
        await _userBufferedPublisher.PublishAsync(userId, data);
    }

    private async Task PersistNotificationAsync(Notification notification, bool serialize,
        CancellationToken cancellationToken)
    {
        if (serialize && _serializer != null)
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
