#region

using System.Collections.Concurrent;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Exceptions;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.DataProtection;
using Polly;
using Serilog;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides services for creating and publishing notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<NotificationService>();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _channelSemaphores = new();
    private readonly IDataProtector _dataProtector;
    private readonly IAsyncPublisher<string, Notification> _publisher;
    private readonly AsyncPolicy _retryPolicy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationService" /> class.
    /// </summary>
    /// <param name="publisher">The async publisher for notifications.</param>
    public NotificationService(
        IAsyncPublisher<string, Notification> publisher, IDataProtectionProvider dataProtectionProvider)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _dataProtector = dataProtectionProvider?.CreateProtector("NotificationProtector")
                         ?? throw new ArgumentNullException(nameof(dataProtectionProvider));


        _retryPolicy = Policy
            .Handle<TransientNotificationException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Logger.Warning(exception,
                        "Transient error publishing notification. Retry attempt {RetryCount} after {RetryInterval}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                })
            .WrapAsync(
                Policy.Handle<PermanentNotificationException>().FallbackAsync(async ct =>
                {
                    Logger.Error("Permanent error publishing notification. No retries will be attempted.");
                    // Perform any necessary fallback actions
                }));
    }

    /// <summary>
    ///     Publishes a notification to a specific channel.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> PublishNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        var channel = GetChannel(false, notification.ChannelId);
        return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
    }

    /// <summary>
    ///     Publishes a notification to the global channel.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> PublishGlobalNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        var channel = GetChannel(true);
        return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
    }

    private static string GetChannel(bool isGlobal, Guid? channelId = null)
    {
        if (isGlobal)
        {
            return GlobalConstants.GlobalNotificationChannel;
        }

        if (channelId == null || channelId == Guid.Empty)
        {
            throw new ArgumentException("ChannelId must be provided for non-global notifications.", nameof(channelId));
        }

        return $"{GlobalConstants.UserNotificationChannel}.{channelId}";
    }


    private async Task<Result> PublishNotificationToChannelAsync(
        Notification notification,
        string channel,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var encryptedNotification = notification;

            // Protect sensitive data
            if (isSensitive)
            {
                // Encrypt sensitive data
                encryptedNotification = EncryptNotification(notification);
            }

            // Ensure thread safety with per-channel semaphore
            var semaphore = _channelSemaphores.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Apply retry policy
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await _publisher.PublishAsync(channel, encryptedNotification, cancellationToken);
                    Logger.Information(
                        "Notification published to channel {Channel} with Type {Type} and Severity {Severity}",
                        channel, notification.Type, notification.Severity);
                });
            }
            finally
            {
                semaphore.Release();
            }

            return Result.Success();
        }
        catch (TransientNotificationException ex)
        {
            Logger.Warning(ex, "Transient error publishing notification to channel {Channel}", channel);
            return Result.Failure($"Transient error: {ex.Message}");
        }
        catch (PermanentNotificationException ex)
        {
            Logger.Error(ex, "Permanent error publishing notification to channel {Channel}", channel);
            return Result.Failure($"Permanent error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error publishing notification to channel {Channel}", channel);
            return Result.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Encrypts sensitive fields of a notification.
    /// </summary>
    /// <param name="notification">The notification to encrypt.</param>
    /// <returns>A new notification with encrypted fields.</returns>
    private Notification EncryptNotification(Notification notification)
    {
        var encryptedMessage = _dataProtector.Protect(notification.Message);
        var encryptedTitle = notification.Title != null ? _dataProtector.Protect(notification.Title) : null;

        var encryptedData = notification.Data;

        if (notification.Data.Count > 0)
        {
            var encryptedDataDict = new Dictionary<string, object?>();
            foreach (var kvp in notification.Data)
            {
                if (kvp.Value is string stringValue)
                {
                    encryptedDataDict[kvp.Key] = _dataProtector.Protect(stringValue);
                }
                else
                {
                    encryptedDataDict[kvp.Key] = kvp.Value; // Non-string data is left as is
                }
            }

            encryptedData = encryptedDataDict;
        }

        return new Notification(
            notification.ChannelId,
            notification.Type,
            notification.Severity,
            encryptedMessage,
            encryptedTitle,
            encryptedData);
    }

    /// <summary>
    ///     Decrypts sensitive fields of a notification.
    /// </summary>
    /// <param name="notification">The notification to decrypt.</param>
    /// <returns>A new notification with decrypted fields.</returns>
    public Notification DecryptNotification(Notification notification)
    {
        var decryptedMessage = _dataProtector.Unprotect(notification.Message);
        var decryptedTitle = notification.Title != null ? _dataProtector.Unprotect(notification.Title) : null;

        var decryptedData = notification.Data;

        if (notification.Data.Count > 0)
        {
            var decryptedDataDict = new Dictionary<string, object?>();
            foreach (var kvp in notification.Data)
            {
                if (kvp.Value is string stringValue)
                {
                    decryptedDataDict[kvp.Key] = _dataProtector.Unprotect(stringValue);
                }
                else
                {
                    decryptedDataDict[kvp.Key] = kvp.Value; // Non-string data is left as is
                }
            }

            decryptedData = decryptedDataDict;
        }

        return new Notification(
            notification.ChannelId,
            notification.Type,
            notification.Severity,
            decryptedMessage,
            decryptedTitle,
            decryptedData);
    }
}
