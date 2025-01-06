#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
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
///     Provides services for creating and publishing notifications to various channels (including a global one),
///     with optional encryption for sensitive data and a retry policy for transient or permanent errors.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<NotificationService>();

    // A thread-safe dictionary mapping channel identifiers to semaphores for concurrency control.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _channelSemaphores = new();

    // For data encryption/decryption if the notification is marked as sensitive.
    private readonly IDataProtector _dataProtector;

    // MessagePipe publisher for asynchronous notifications.
    private readonly IAsyncPublisher<string, Notification> _publisher;

    // Polly-based retry policy that handles transient and permanent notification exceptions differently.
    private readonly AsyncPolicy _retryPolicy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationService" /> class.
    /// </summary>
    /// <param name="publisher">The async publisher for notifications (injected by DI).</param>
    /// <param name="dataProtectionProvider">
    ///     The provider for data protection services (used to create a <see cref="IDataProtector" />).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="publisher" /> or <paramref name="dataProtectionProvider" /> is null.
    /// </exception>
    public NotificationService(
        IAsyncPublisher<string, Notification> publisher,
        IDataProtectionProvider dataProtectionProvider)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        if (dataProtectionProvider is null)
        {
            throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        // Create a named protector for notifications
        _dataProtector = dataProtectionProvider.CreateProtector("NotificationProtector");

        // Define a retry policy:
        //  - Handle TransientNotificationException with WaitAndRetry
        //  - Wrap with fallback for PermanentNotificationException
        _retryPolicy = Policy
            .Handle<TransientNotificationException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                (exception, timeSpan, retryCount, context) =>
                {
                    Logger.Warning(
                        exception,
                        "Transient error publishing notification. Retry attempt {RetryCount} after {RetryInterval}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                })
            .WrapAsync(
                Policy.Handle<PermanentNotificationException>()
                    .FallbackAsync(async ct =>
                    {
                        Logger.Error("Permanent error publishing notification. No retries will be attempted.");
                        // Perform any necessary fallback actions (e.g., logging or alerting).
                    }));
    }

    /// <summary>
    ///     Publishes a notification to the specified (non-global) channel.
    /// </summary>
    /// <param name="notification">The notification to publish (must not be null).</param>
    /// <param name="isSensitive">
    ///     If <c>true</c>, the notification's <see cref="Notification.Message" /> and other string data
    ///     will be encrypted before publishing.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the publish operation if needed.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification" /> is null.</exception>
    public async Task<Result> PublishNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        // Build the channel ID from the notification, ensuring it's not global.
        var channel = GetChannel(false, notification.ChannelId);
        return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
    }

    /// <summary>
    ///     Publishes a notification to the global channel, so all subscribers to that channel receive it.
    /// </summary>
    /// <param name="notification">The notification to publish (must not be null).</param>
    /// <param name="isSensitive">
    ///     If <c>true</c>, the notification's <see cref="Notification.Message" /> and other string data
    ///     will be encrypted before publishing.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the publish operation if needed.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification" /> is null.</exception>
    public async Task<Result> PublishGlobalNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        // Build the channel ID for the global channel.
        var channel = GetChannel(true);
        return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
    }

    /// <summary>
    ///     Decrypts a previously encrypted notification (i.e., if <see cref="PublishNotificationAsync" />
    ///     or <see cref="PublishGlobalNotificationAsync" /> was called with <paramref name="isSensitive" /> = true).
    /// </summary>
    /// <param name="notification">The notification to decrypt.</param>
    /// <returns>
    ///     A new <see cref="Notification" /> instance with <see cref="Notification.Message" /> (and other string fields)
    ///     decrypted.
    /// </returns>
    public Notification DecryptNotification(Notification notification)
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        var decryptedMessage = _dataProtector.Unprotect(notification.Message);
        var decryptedTitle = notification.Title != null ? _dataProtector.Unprotect(notification.Title) : null;

        var decryptedData = notification.Data;
        if (notification.Data.Count > 0)
        {
            var decryptedDataDict = new Dictionary<string, object?>();
            foreach (var kvp in notification.Data)
            {
                // If the value is a string, decrypt it; otherwise leave it.
                if (kvp.Value is string stringValue)
                {
                    decryptedDataDict[kvp.Key] = _dataProtector.Unprotect(stringValue);
                }
                else
                {
                    decryptedDataDict[kvp.Key] = kvp.Value;
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

    /// <summary>
    ///     Derives a channel name based on whether it's global or user-specific.
    ///     For user channels, <paramref name="channelId" /> must not be null or empty.
    /// </summary>
    /// <param name="isGlobal">If <c>true</c>, returns the global notification channel name.</param>
    /// <param name="channelId">If not global, this must be a valid <see cref="Guid" /> representing a user channel.</param>
    /// <returns>A string representing the channel name.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="isGlobal" /> is <c>false</c> but <paramref name="channelId" /> is null or empty.
    /// </exception>
    private static string GetChannel(bool isGlobal, Guid? channelId = null)
    {
        if (isGlobal)
        {
            // Return a predefined global channel name
            return GlobalConstants.GlobalNotificationChannel;
        }

        if (channelId == null || channelId == Guid.Empty)
        {
            throw new ArgumentException("ChannelId must be provided for non-global notifications.", nameof(channelId));
        }

        return $"{GlobalConstants.UserNotificationChannel}.{channelId}";
    }

    /// <summary>
    ///     Publishes the <paramref name="notification" /> to the specified <paramref name="channel" />,
    ///     optionally encrypting it first if <paramref name="isSensitive" /> is set.
    ///     Uses a semaphore per channel to ensure only one publish at a time per channel,
    ///     and a <see cref="Polly" /> retry policy for transient/permanent errors.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="channel">The target channel name.</param>
    /// <param name="isSensitive">If <c>true</c>, the notification is encrypted before sending.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    private async Task<Result> PublishNotificationToChannelAsync(
        Notification notification,
        string channel,
        bool isSensitive,
        CancellationToken cancellationToken)
    {
        try
        {
            // If the notification is marked sensitive, encrypt any string data.
            var encryptedNotification = isSensitive ? EncryptNotification(notification) : notification;

            // Acquire a semaphore specific to the channel.
            var semaphore = _channelSemaphores.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Execute under the retry policy
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
    ///     Encrypts potentially sensitive fields (message, title, string-based <see cref="Notification.Data" /> values)
    ///     of the given <paramref name="notification" />.
    /// </summary>
    /// <param name="notification">The original notification with unencrypted data.</param>
    /// <returns>A new <see cref="Notification" /> instance with certain fields encrypted via <see cref="_dataProtector" />.</returns>
    private Notification EncryptNotification(Notification notification)
    {
        // Encryption for the main message
        var encryptedMessage = _dataProtector.Protect(notification.Message);

        // Title might be null, so only protect it if present
        var encryptedTitle = notification.Title != null
            ? _dataProtector.Protect(notification.Title)
            : null;

        // If the Data dictionary contains string values, encrypt them individually
        var encryptedData = notification.Data;
        if (notification.Data.Count > 0)
        {
            var encryptedDataDict = new Dictionary<string, object?>();
            foreach (var kvp in notification.Data)
            {
                if (kvp.Value is string stringValue)
                {
                    // Only strings are encrypted; other data types remain as-is
                    encryptedDataDict[kvp.Key] = _dataProtector.Protect(stringValue);
                }
                else
                {
                    encryptedDataDict[kvp.Key] = kvp.Value;
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
}
