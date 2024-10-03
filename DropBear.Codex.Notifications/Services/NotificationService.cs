#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Polly;
using Serilog;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides services for creating and publishing notifications.
/// </summary>
public sealed class NotificationService
{
    private const string GlobalNotificationChannel = "GlobalNotificationChannel";
    private readonly ILogger _logger;
    private readonly IAsyncPublisher<string, Notification> _publisher;
    private readonly AsyncPolicy _retryPolicy;

    /// <summary>
    ///     Initializes a new instance of the NotificationService class.
    /// </summary>
    public NotificationService(IAsyncPublisher<string, Notification> publisher)
    {
        _logger = LoggerFactory.Logger.ForContext<NotificationService>();
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.Warning(exception,
                        "Error publishing notification. Retry attempt {RetryCount} after {RetryInterval}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                });
    }

    private static string GetChannel(Guid channelId)
    {
        return $"{GlobalNotificationChannel}.{channelId}";
    }

    /// <summary>
    ///     Creates a new notification with the specified parameters.
    /// </summary>
    public static Result<Notification> CreateNotification(Guid channelId, NotificationType type,
        NotificationSeverity severity,
        string message, string? title = null, Dictionary<string, object?>? data = null)
    {
        if (channelId == Guid.Empty)
        {
            return Result<Notification>.Failure("ChannelId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return Result<Notification>.Failure("Message cannot be null or whitespace.");
        }

        if (type == NotificationType.NotSpecified || severity == NotificationSeverity.NotSpecified)
        {
            return Result<Notification>.Failure("NotificationType or NotificationSeverity must be specified.");
        }

        return Result<Notification>.Success(new Notification(channelId, type, severity, message, title, data));
    }

    private async Task<Result> PublishNotificationInternalAsync(Notification? notification, string channel,
        CancellationToken cancellationToken = default)
    {
        if (notification is null)
        {
            return Result.Failure("Notification cannot be null.");
        }

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _publisher.PublishAsync(channel, notification, cancellationToken);
                _logger.Information("Notification published successfully to channel {Channel}", channel);
            });
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error publishing notification to channel {Channel} after all retry attempts.", channel);
            return Result.Failure($"Failed to publish notification: {ex.Message}");
        }
    }

    /// <summary>
    ///     Publishes a notification to its specific channel.
    /// </summary>
    public Task<Result> PublishNotificationAsync(Notification? notification,
        CancellationToken cancellationToken = default)
    {
        return PublishNotificationInternalAsync(notification, GetChannel(notification?.ChannelId ?? Guid.Empty),
            cancellationToken);
    }

    /// <summary>
    ///     Publishes a notification to the global channel.
    /// </summary>
    public Task<Result> PublishGlobalNotificationAsync(Notification? notification,
        CancellationToken cancellationToken = default)
    {
        return PublishNotificationInternalAsync(notification, GlobalNotificationChannel, cancellationToken);
    }
}
