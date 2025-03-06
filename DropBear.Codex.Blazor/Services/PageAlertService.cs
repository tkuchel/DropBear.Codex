#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing page alerts with optimized batching, rate limiting,
///     and memory usage for Blazor Server applications.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the PageAlertService.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public PageAlertService(ILogger<PageAlertService>? logger = null)
    {
        _logger = logger;
        _pendingAlerts = new ConcurrentQueue<PageAlertInstance>();
        _cts = new CancellationTokenSource();
        _eventSemaphore = new SemaphoreSlim(1, 1);
        _alertsQueued = 0;

        _logger?.LogDebug("PageAlertService initialized");
    }

    #endregion

    #region Fields and Constants

    /// <summary>
    ///     Default duration for success alerts in milliseconds.
    /// </summary>
    private const int DefaultSuccessDuration = 5000;

    /// <summary>
    ///     Default duration for error alerts in milliseconds.
    /// </summary>
    private const int DefaultErrorDuration = 8000;

    /// <summary>
    ///     Default duration for warning alerts in milliseconds.
    /// </summary>
    private const int DefaultWarningDuration = 6000;

    /// <summary>
    ///     Default duration for information alerts in milliseconds.
    /// </summary>
    private const int DefaultInfoDuration = 5000;

    /// <summary>
    ///     Maximum number of alerts allowed in the queue.
    /// </summary>
    private const int MaxQueueSize = 100;

    /// <summary>
    ///     Maximum number of retries for processing alerts.
    /// </summary>
    private const int MaxProcessingRetries = 3;

    /// <summary>
    ///     Timeout for alert operations in milliseconds.
    /// </summary>
    private const int OperationTimeoutMs = 5000;

    /// <summary>
    ///     Thread-safe queue for pending alerts.
    /// </summary>
    private readonly ConcurrentQueue<PageAlertInstance> _pendingAlerts;

    /// <summary>
    ///     Cancellation token source for controlling async operations during shutdown.
    /// </summary>
    private readonly CancellationTokenSource _cts;

    /// <summary>
    ///     Lock for synchronizing event handling.
    /// </summary>
    private readonly SemaphoreSlim _eventSemaphore;

    /// <summary>
    ///     Logger for diagnostic information.
    /// </summary>
    private readonly ILogger<PageAlertService>? _logger;

    /// <summary>
    ///     Tracks the number of queued alerts to enforce limits atomically.
    /// </summary>
    private long _alertsQueued;

    /// <summary>
    ///     Flag indicating whether the service is currently processing alerts.
    /// </summary>
    private int _isProcessing;

    /// <summary>
    ///     Flag indicating whether the service has been disposed.
    /// </summary>
    private int _isDisposed;

    #endregion

    #region Events

    /// <summary>
    ///     Event fired when a new alert should be shown.
    /// </summary>
    public event Func<PageAlertInstance, Task>? OnAlert;

    /// <summary>
    ///     Event fired when all alerts should be cleared.
    /// </summary>
    public event Action? OnClear;

    #endregion

    #region Public Async Methods

    /// <summary>
    ///     Shows a general alert with the specified parameters.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="type">Type of the alert.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<Unit, AlertError>> ShowAlertAsync(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = null,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        // Check queue limit early to avoid creating alert objects unnecessarily
        if (Interlocked.Read(ref _alertsQueued) >= MaxQueueSize)
        {
            _logger?.LogWarning("Alert queue is full. Dropping alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Alert queue is full"));
        }

        try
        {
            // Create a linked token source for operation timeout and cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token,
                cancellationToken);

            // Set timeout for the operation
            linkedCts.CancelAfter(OperationTimeoutMs);

            var alertId = $"alert-{Guid.NewGuid():N}";

            var alert = new PageAlertInstance
            {
                Id = alertId,
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Type = type,
                Duration = duration ?? GetDefaultDuration(type),
                IsPermanent = isPermanent,
                CreatedAt = DateTime.UtcNow
            };

            // Atomically increment queued alert count
            if (Interlocked.Increment(ref _alertsQueued) > MaxQueueSize)
            {
                // Decrement and return error if we exceeded the limit
                Interlocked.Decrement(ref _alertsQueued);
                _logger?.LogWarning("Alert queue is full. Dropping alert: {Title}", title);
                return Result<Unit, AlertError>.Failure(
                    AlertError.CreateFailed("Alert queue is full"));
            }

            _pendingAlerts.Enqueue(alert);

            // Start processing alerts, but don't await it
            _ = TryProcessPendingAlertsAsync();

            return Result<Unit, AlertError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("ShowAlert operation cancelled for alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error creating alert: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Shows a success alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowSuccessAsync(
        string title,
        string message,
        int? duration = DefaultSuccessDuration,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Success, duration, false, cancellationToken);
    }

    /// <summary>
    ///     Shows an error alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowErrorAsync(
        string title,
        string message,
        int? duration = DefaultErrorDuration,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Error, duration, false, cancellationToken);
    }

    /// <summary>
    ///     Shows a warning alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowWarningAsync(
        string title,
        string message,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(
            title,
            message,
            PageAlertType.Warning,
            isPermanent ? null : DefaultWarningDuration,
            isPermanent,
            cancellationToken
        );
    }

    /// <summary>
    ///     Shows an info alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowInfoAsync(
        string title,
        string message,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(
            title,
            message,
            PageAlertType.Info,
            isPermanent ? null : DefaultInfoDuration,
            isPermanent,
            cancellationToken
        );
    }

    /// <summary>
    ///     Shows multiple alerts in a batch operation.
    /// </summary>
    /// <param name="alerts">Collection of alerts to show.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success, partial success, or failure.</returns>
    public async Task<Result<Unit, AlertError>> ShowAlertBatchAsync(
        IEnumerable<PageAlertInstance> alerts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            // Create a linked token source for operation timeout and cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token,
                cancellationToken);

            // Set timeout for the operation
            linkedCts.CancelAfter(OperationTimeoutMs);

            var alertList = alerts.ToList();
            if (alertList.Count == 0)
            {
                return Result<Unit, AlertError>.Success(Unit.Value);
            }

            var enqueued = 0;
            var rejected = 0;

            // Check if we have room in the queue
            var currentQueueSize = Interlocked.Read(ref _alertsQueued);
            var availableSlots = MaxQueueSize - currentQueueSize;

            if (availableSlots <= 0)
            {
                _logger?.LogWarning("Alert queue is full. Dropping alert batch with {Count} alerts", alertList.Count);
                return Result<Unit, AlertError>.Failure(
                    AlertError.CreateFailed("Alert queue is full"));
            }

            // Only enqueue as many alerts as we have slots for
            var alertsToEnqueue = (int)Math.Min(availableSlots, alertList.Count);

            // Adjust queue count all at once
            Interlocked.Add(ref _alertsQueued, alertsToEnqueue);

            // Enqueue the alerts
            for (var i = 0; i < alertsToEnqueue; i++)
            {
                if (linkedCts.Token.IsCancellationRequested)
                {
                    // Remove any alerts we counted but didn't enqueue
                    Interlocked.Add(ref _alertsQueued, -(alertsToEnqueue - enqueued));
                    break;
                }

                var alert = alertList[i];

                // Ensure alert has an ID
                // if (string.IsNullOrEmpty(alert.Id))
                // {
                //     alert.Id = $"alert-{Guid.NewGuid():N}";
                // }

                // Ensure CreatedAt is set
                // if (alert.CreatedAt == default)
                // {
                //     alert.CreatedAt = DateTime.UtcNow;
                // }

                _pendingAlerts.Enqueue(alert);
                enqueued++;
            }

            rejected = alertList.Count - enqueued;

            // Start processing alerts
            _ = TryProcessPendingAlertsAsync();

            if (rejected > 0)
            {
                _logger?.LogWarning("Rejected {RejectedCount} alerts due to queue size limits", rejected);
                return Result<Unit, AlertError>.PartialSuccess(
                    Unit.Value,
                    AlertError.CreateFailed($"Queue limit reached. {enqueued} alerts enqueued, {rejected} rejected"));
            }

            return Result<Unit, AlertError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("ShowAlertBatch operation cancelled");
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating alert batch");
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error creating alert batch: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Clears all displayed alerts.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<Unit, AlertError>> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            // Create a linked token source for operation timeout and cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token,
                cancellationToken);

            // Set timeout for the operation
            linkedCts.CancelAfter(OperationTimeoutMs);

            var semaphoreAcquired = false;
            try
            {
                semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(2), linkedCts.Token);
                if (!semaphoreAcquired)
                {
                    _logger?.LogWarning("Timeout waiting for semaphore in ClearAsync");
                    return Result<Unit, AlertError>.Failure(
                        AlertError.CreateFailed("Timeout waiting to clear alerts"));
                }

                // Clear pending queue and reset count
                var removedCount = 0;
                while (_pendingAlerts.TryDequeue(out _))
                {
                    removedCount++;
                }

                // Reset our alert counter
                Interlocked.Exchange(ref _alertsQueued, 0);

                // Invoke clear event
                OnClear?.Invoke();

                _logger?.LogDebug("Cleared {Count} pending alerts", removedCount);

                return Result<Unit, AlertError>.Success(Unit.Value);
            }
            finally
            {
                if (semaphoreAcquired && !linkedCts.IsCancellationRequested)
                {
                    _eventSemaphore.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Operation was canceled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing alerts");
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error clearing alerts: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Gets service metrics for monitoring.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A result containing metrics dictionary.</returns>
    public async Task<Result<IDictionary<string, object>, AlertError>> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<IDictionary<string, object>, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            var metrics = new Dictionary<string, object>
            {
                ["PendingAlertCount"] = Interlocked.Read(ref _alertsQueued),
                ["QueueCapacity"] = MaxQueueSize,
                ["IsProcessing"] = Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1,
                ["IsDisposed"] = Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1,
                ["HasAlertSubscribers"] = OnAlert != null,
                ["HasClearSubscribers"] = OnClear != null,
                ["Timestamp"] = DateTime.UtcNow
            };

            return Result<IDictionary<string, object>, AlertError>.Success(metrics);
        }
        catch (OperationCanceledException)
        {
            return Result<IDictionary<string, object>, AlertError>.Failure(
                AlertError.CreateFailed("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting metrics");
            return Result<IDictionary<string, object>, AlertError>.Failure(
                AlertError.CreateFailed($"Error getting metrics: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Disposes the service and its resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        try
        {
            _logger?.LogDebug("Disposing PageAlertService");

            // Cancel any pending operations
            await _cts.CancelAsync();

            // Clear pending queue to prevent memory leaks
            var semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(1));
            try
            {
                while (_pendingAlerts.TryDequeue(out _)) { }

                Interlocked.Exchange(ref _alertsQueued, 0);
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _eventSemaphore.Release();
                }
            }

            // Dispose resources
            _cts.Dispose();
            _eventSemaphore.Dispose();

            // Clear event handlers to prevent memory leaks
            OnAlert = null;
            OnClear = null;

            _logger?.LogDebug("PageAlertService successfully disposed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing PageAlertService");
        }
    }

    #endregion

    #region Compatibility Synchronous Methods

    /// <summary>
    ///     Shows a general alert (legacy sync method).
    /// </summary>
    public void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false)
    {
        // Fire-and-forget async operation with error handling
        _ = ShowAlertAsync(title, message, type, duration, isPermanent)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in ShowAlert fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Shows a success alert (legacy sync method).
    /// </summary>
    public void ShowSuccess(string title, string message, int? duration = DefaultSuccessDuration)
    {
        _ = ShowSuccessAsync(title, message, duration)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in ShowSuccess fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Shows an error alert (legacy sync method).
    /// </summary>
    public void ShowError(string title, string message, int? duration = DefaultErrorDuration)
    {
        _ = ShowErrorAsync(title, message, duration)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in ShowError fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Shows a warning alert (legacy sync method).
    /// </summary>
    public void ShowWarning(string title, string message, bool isPermanent = false)
    {
        _ = ShowWarningAsync(title, message, isPermanent)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in ShowWarning fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Shows an info alert (legacy sync method).
    /// </summary>
    public void ShowInfo(string title, string message, bool isPermanent = false)
    {
        _ = ShowInfoAsync(title, message, isPermanent)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in ShowInfo fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Clears all displayed alerts (legacy sync method).
    /// </summary>
    public void Clear()
    {
        _ = ClearAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(task.Exception, "Error in Clear fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Process pending alerts in a thread-safe manner
    /// </summary>
    private async Task TryProcessPendingAlertsAsync()
    {
        // Only one processing loop should be active at a time
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 1)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1 || OnAlert == null)
        {
            Interlocked.Exchange(ref _isProcessing, 0);
            return;
        }

        var semaphoreAcquired = false;
        try
        {
            for (var retry = 0; retry < MaxProcessingRetries; retry++)
            {
                try
                {
                    // Try to acquire the semaphore with a reasonable timeout
                    semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(2), _cts.Token);
                    if (!semaphoreAcquired)
                    {
                        // If we can't acquire the semaphore, log and break the retry loop
                        _logger?.LogWarning("Timeout waiting for semaphore in TryProcessPendingAlertsAsync");
                        break;
                    }

                    // Process all pending alerts
                    var processedCount = 0;
                    while (_pendingAlerts.TryDequeue(out var alert))
                    {
                        try
                        {
                            // Decrement our counter
                            Interlocked.Decrement(ref _alertsQueued);
                            processedCount++;

                            await OnAlert.Invoke(alert);

                            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                            {
                                _logger.LogDebug("Alert processed: {AlertId} - {Title}", alert.Id, alert.Title);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error processing alert: {AlertId}", alert.Id);
                        }
                    }

                    if (processedCount > 0 && _logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        _logger.LogDebug("Processed {Count} alerts", processedCount);
                    }

                    // Processing succeeded, break the retry loop
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown, break the retry loop
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing pending alerts (attempt {Retry})", retry + 1);

                    // If we've acquired the semaphore but encountered an error, release it
                    if (semaphoreAcquired && !_cts.IsCancellationRequested)
                    {
                        _eventSemaphore.Release();
                        semaphoreAcquired = false;
                    }

                    // Short delay before retry
                    await Task.Delay(50, _cts.Token);
                }
            }
        }
        finally
        {
            // Ensure semaphore is released if we acquired it
            if (semaphoreAcquired && !_cts.IsCancellationRequested)
            {
                _eventSemaphore.Release();
            }

            // Reset processing flag
            Interlocked.Exchange(ref _isProcessing, 0);

            // If there are still items in the queue, trigger another processing pass
            if (!_pendingAlerts.IsEmpty && !_cts.IsCancellationRequested &&
                Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 0)
            {
                _ = TryProcessPendingAlertsAsync();
            }
        }
    }

    /// <summary>
    ///     Returns the default duration based on alert type
    /// </summary>
    private static int GetDefaultDuration(PageAlertType type)
    {
        return type switch
        {
            PageAlertType.Success => DefaultSuccessDuration,
            PageAlertType.Error => DefaultErrorDuration,
            PageAlertType.Warning => DefaultWarningDuration,
            PageAlertType.Info => DefaultInfoDuration,
            _ => DefaultInfoDuration
        };
    }

    #endregion
}
