#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Exceptions;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

#endregion

namespace DropBear.Codex.Notifications.Services;



/// <summary>
///     Provides services for creating and publishing notifications to various channels (including a global one),
///     with optional encryption for sensitive data and a retry policy for transient or permanent errors.
/// </summary>
public sealed class NotificationService : INotificationService, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<NotificationService>();

    // A thread-safe dictionary mapping channel identifiers to semaphores for concurrency control and tracking usage.
    private readonly ConcurrentDictionary<string, (SemaphoreSlim Semaphore, DateTime LastUsed)> _channelSemaphores =
        new();

    // Timer for cleanup of unused semaphores.
    private readonly Timer _cleanupTimer;

    // For data encryption/decryption if the notification is marked as sensitive.
    private readonly IDataProtector _dataProtector;

    // Optional notification pool.
    private readonly NotificationPool? _notificationPool;

    // Configuration options.
    private readonly NotificationServiceOptions _options;

    // MessagePipe publisher for asynchronous notifications.
    private readonly IAsyncPublisher<string, Notification> _publisher;

    // Polly-based retry policy that handles transient and permanent notification exceptions differently.
    private readonly AsyncPolicy _retryPolicy;

    // Telemetry service for tracking result operations.
    private readonly IResultTelemetry _telemetry;

    // Time abstraction for testability.
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationService" /> class.
    /// </summary>
    /// <param name="publisher">The async publisher for notifications (injected by DI).</param>
    /// <param name="dataProtectionProvider">The provider for data protection services.</param>
    /// <param name="telemetry">The telemetry service for tracking result operations.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="notificationPool">Optional notification pool for object reuse.</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="publisher" />, <paramref name="dataProtectionProvider" />,
    ///     or <paramref name="telemetry" /> is null.
    /// </exception>
    public NotificationService(
        IAsyncPublisher<string, Notification> publisher,
        IDataProtectionProvider dataProtectionProvider,
        IResultTelemetry telemetry,
        IOptions<NotificationServiceOptions>? options = null,
        NotificationPool? notificationPool = null,
        TimeProvider? timeProvider = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _options = options?.Value ?? new NotificationServiceOptions();
        _notificationPool = notificationPool;

        if (dataProtectionProvider is null)
        {
            throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        // Create a named protector for notifications
        _dataProtector = dataProtectionProvider.CreateProtector("NotificationProtector");

        // Configure the retry policy with circuit breaker
        _retryPolicy = ConfigureRetryPolicy();

        // Start the cleanup timer
        _cleanupTimer = new Timer(
            CleanupUnusedSemaphores,
            null,
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(10));
    }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();

        // Dispose all semaphores
        foreach (var (semaphore, _) in _channelSemaphores.Values)
        {
            semaphore.Dispose();
        }

        _channelSemaphores.Clear();
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
    /// <returns>A <see cref="Result{Unit, NotificationError}" /> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification" /> is null.</exception>
    public async Task<Result<Unit, NotificationError>> PublishNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        try
        {
            // Build the channel ID from the notification, ensuring it's not global
            var channel = GetChannel(false, notification.ChannelId);
            return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Error publishing notification {NotificationType} with severity {Severity}",
                notification.Type, notification.Severity);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex)
                    .WithContext(nameof(PublishNotificationAsync)));
        }
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
    /// <returns>A <see cref="Result{Unit, NotificationError}" /> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification" /> is null.</exception>
    public async Task<Result<Unit, NotificationError>> PublishGlobalNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        try
        {
            // Build the channel ID for the global channel
            var channel = GetChannel(true);
            return await PublishNotificationToChannelAsync(notification, channel, isSensitive, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Error publishing global notification {NotificationType} with severity {Severity}",
                notification.Type, notification.Severity);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(
                NotificationError.FromException(ex)
                    .WithContext(nameof(PublishGlobalNotificationAsync)));
        }
    }

    /// <summary>
    ///     Decrypts a previously encrypted notification (i.e., if <see cref="PublishNotificationAsync" />
    ///     or <see cref="PublishGlobalNotificationAsync" /> was called with <paramref name="isSensitive" /> = true).
    /// </summary>
    /// <param name="notification">The notification to decrypt.</param>
    /// <returns>
    ///     A result containing the decrypted notification or an error.
    /// </returns>
    public Result<Notification, NotificationError> DecryptNotification(Notification notification)
    {
        if (notification is null)
        {
            var error = new NotificationError("Cannot decrypt a null notification.")
                .WithContext(nameof(DecryptNotification));
            return Result<Notification, NotificationError>.Failure(error);
        }

        try
        {
            var decryptedMessage = _dataProtector.Unprotect(notification.Message);
            var decryptedTitle = notification.Title != null ? _dataProtector.Unprotect(notification.Title) : null;

            // Only create a new dictionary if we have string values to decrypt
            Dictionary<string, object?>? decryptedDataDict = null;

            if (notification.Data.Any(kvp => kvp.Value is string))
            {
                decryptedDataDict = new Dictionary<string, object?>(notification.Data.Count, StringComparer.Ordinal);
                foreach (var kvp in notification.Data)
                {
                    // If the value is a string, decrypt it; otherwise leave it
                    decryptedDataDict[kvp.Key] = kvp.Value is string stringValue
                        ? _dataProtector.Unprotect(stringValue)
                        : kvp.Value;
                }
            }

            // Create a new notification with the decrypted values
            Notification decryptedNotification;
            if (_notificationPool != null)
            {
                // If we have a pool, use it
                decryptedNotification = _notificationPool.Get(
                    notification.ChannelId,
                    notification.Type,
                    notification.Severity,
                    decryptedMessage,
                    decryptedTitle,
                    decryptedDataDict);
            }
            else
            {
                // Otherwise create a new instance
                decryptedNotification = new Notification(
                    notification.ChannelId,
                    notification.Type,
                    notification.Severity,
                    decryptedMessage,
                    decryptedTitle,
                    decryptedDataDict);
            }

            return Result<Notification, NotificationError>.Success(decryptedNotification);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error decrypting notification");
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Notification, NotificationError>.Failure(
                NotificationError.FromException(ex)
                    .WithContext(nameof(DecryptNotification)));
        }
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
    /// <returns>A <see cref="Result{Unit, NotificationError}" /> indicating success or failure.</returns>
    private async Task<Result<Unit, NotificationError>> PublishNotificationToChannelAsync(
        Notification notification,
        string channel,
        bool isSensitive,
        CancellationToken cancellationToken)
    {
        try
        {
            // Skip if we're already canceled
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Notification publishing was canceled.", cancellationToken);
            }

            // If the notification is marked sensitive, encrypt any string data
            Notification notificationToPublish;
            if (isSensitive)
            {
                var encryptResult = EncryptNotification(notification);
                if (!encryptResult.IsSuccess)
                {
                    return Result<Unit, NotificationError>.Failure(encryptResult.Error, encryptResult.Exception);
                }
                notificationToPublish = encryptResult.Value!;
            }
            else
            {
                notificationToPublish = notification;
            }

            // Acquire a semaphore specific to the channel
            var nowUtc = DateTime.UtcNow; // Convert DateTimeOffset to DateTime
            var (semaphore, _) = _channelSemaphores.AddOrUpdate(
                channel,
                // If not present, create a new semaphore
                _ => (new SemaphoreSlim(1, 1), nowUtc),
                // If present, update the last used time
                (_, existing) => (existing.Semaphore, nowUtc));

            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Execute under the retry policy
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await _publisher.PublishAsync(channel, notificationToPublish, cancellationToken);

                    Logger.Information(
                        "Notification published to channel {Channel} with Type {Type} and Severity {Severity}",
                        channel, notification.Type, notification.Severity);
                });

                _telemetry.TrackResultCreated(ResultState.Success, GetType());
                return Result<Unit, NotificationError>.Success(Unit.Value);
            }
            finally
            {
                // Always release the semaphore
                semaphore.Release();

                // Update the last used time
                if (_channelSemaphores.TryGetValue(channel, out var value))
                {
                    _channelSemaphores[channel] = (value.Semaphore, DateTime.UtcNow);
                }
            }
        }
        catch (TransientNotificationException ex)
        {
            Logger.Warning(ex, "Transient error publishing notification to channel {Channel}", channel);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(
                new NotificationError(ex.Message)
                {
                    IsTransient = true,
                    Severity = NotificationSeverity.Warning,
                    Context = nameof(PublishNotificationToChannelAsync)
                });
        }
        catch (PermanentNotificationException ex)
        {
            Logger.Error(ex, "Permanent error publishing notification to channel {Channel}", channel);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(
                new NotificationError(ex.Message)
                {
                    IsTransient = false,
                    Severity = NotificationSeverity.Error,
                    Context = nameof(PublishNotificationToChannelAsync)
                });
        }
        catch (BrokenCircuitException ex)
        {
            Logger.Error(ex, "Circuit breaker open when publishing to channel {Channel}", channel);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(
                new NotificationError("Too many notification failures. Circuit breaker is open.")
                {
                    IsTransient = true,
                    Severity = NotificationSeverity.Error,
                    Context = nameof(PublishNotificationToChannelAsync)
                });
        }
        catch (OperationCanceledException ex)
        {
            Logger.Warning(ex, "Notification publishing canceled for channel {Channel}", channel);
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error publishing notification to channel {Channel}", channel);
            _telemetry.TrackException(ex, ResultState.Failure, GetType());

            return Result<Unit, NotificationError>.Failure(NotificationError.FromException(ex)
                .WithContext(nameof(PublishNotificationToChannelAsync)));
        }
    }

    /// <summary>
    ///     Encrypts potentially sensitive fields (message, title, string-based <see cref="Notification.Data" /> values)
    ///     of the given <paramref name="notification" />.
    /// </summary>
    /// <param name="notification">The original notification with unencrypted data.</param>
    /// <returns>A new <see cref="Notification" /> instance with certain fields encrypted via <see cref="_dataProtector" />.</returns>
    private Result<Notification, NotificationError> EncryptNotification(Notification notification)
    {
        try
        {
            // Encryption for the main message
            var encryptedMessage = _dataProtector.Protect(notification.Message);

            // Title might be null, so only protect it if present
            var encryptedTitle = notification.Title != null
                ? _dataProtector.Protect(notification.Title)
                : null;

            // Only create a new dictionary if we have string values to encrypt
            Dictionary<string, object?>? encryptedDataDict = null;

            if (notification.Data.Any(kvp => kvp.Value is string))
            {
                encryptedDataDict = new Dictionary<string, object?>(notification.Data.Count, StringComparer.Ordinal);
                foreach (var kvp in notification.Data)
                {
                    encryptedDataDict[kvp.Key] = kvp.Value is string stringValue
                        ? _dataProtector.Protect(stringValue)
                        : kvp.Value;
                }
            }

            // Create a new notification with the encrypted values
            Notification encryptedNotification;
            if (_notificationPool != null)
            {
                // If we have a pool, use it
                encryptedNotification = _notificationPool.Get(
                    notification.ChannelId,
                    notification.Type,
                    notification.Severity,
                    encryptedMessage,
                    encryptedTitle,
                    encryptedDataDict);
            }
            else
            {
                // Otherwise create a new instance
                encryptedNotification = new Notification(
                    notification.ChannelId,
                    notification.Type,
                    notification.Severity,
                    encryptedMessage,
                    encryptedTitle,
                    encryptedDataDict);
            }

            return Result<Notification, NotificationError>.Success(encryptedNotification);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error encrypting notification");
            return Result<Notification, NotificationError>.Failure(
                NotificationError.EncryptionFailed(ex.Message)
                    .WithContext(nameof(EncryptNotification)),
                ex);
        }
    }

    /// <summary>
    ///     Configures and returns the Polly retry policy with circuit breaker.
    /// </summary>
    /// <returns>The configured AsyncPolicy.</returns>
    private AsyncPolicy ConfigureRetryPolicy()
    {
        return Policy
            .Handle<TransientNotificationException>()
            .WaitAndRetryAsync(
                _options.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                (exception, timeSpan, retryCount, context) =>
                {
                    Logger.Warning(
                        exception,
                        "Transient error publishing notification. Retry attempt {RetryCount} after {RetryInterval}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                })
            .WrapAsync(
                Policy.Handle<Exception>()
                    .CircuitBreakerAsync(
                        _options.CircuitBreakerThreshold,
                        TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                        (ex, breakDelay) =>
                        {
                            Logger.Error(ex, "Circuit breaker opened for {BreakDelay}ms", breakDelay.TotalMilliseconds);
                            _telemetry.TrackException(ex, ResultState.Failure, GetType());
                        },
                        () =>
                        {
                            Logger.Information("Circuit breaker reset");
                        },
                        () =>
                        {
                            Logger.Information("Circuit breaker half-open");
                        }
                    )
            )
            .WrapAsync(
                Policy.Handle<PermanentNotificationException>()
                    .FallbackAsync(async ct =>
                    {
                        Logger.Error("Permanent error publishing notification. No retries will be attempted.");
                    }));
    }

    /// <summary>
    ///     Cleans up unused semaphores to prevent memory leaks.
    /// </summary>
    /// <param name="state">State object (not used).</param>
    private void CleanupUnusedSemaphores(object? state)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-_options.SemaphoreTimeoutMinutes);
            var keysToRemove = _channelSemaphores
                .Where(kvp => kvp.Value.LastUsed < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_channelSemaphores.TryRemove(key, out var value))
                {
                    value.Semaphore.Dispose();
                    Logger.Debug("Disposed unused semaphore for channel {Channel}", key);
                }
            }

            if (keysToRemove.Count > 0)
            {
                Logger.Information("Cleaned up {Count} unused semaphores", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cleaning up unused semaphores");
        }
    }
}
