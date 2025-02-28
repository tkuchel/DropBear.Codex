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
///     Thread-safe service for managing page alerts with batching and rate limiting support.
///     Optimized for Blazor Server environments with Result pattern integration.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    private const int DefaultSuccessDuration = 5000;
    private const int DefaultErrorDuration = 8000;
    private const int DefaultWarningDuration = 6000;
    private const int DefaultInfoDuration = 5000;
    private const int MaxQueueSize = 100;
    private const int MaxProcessingRetries = 3;

    private readonly CancellationTokenSource _cts;
    private readonly SemaphoreSlim _eventSemaphore;
    private readonly ILogger<PageAlertService>? _logger;
    private readonly ConcurrentQueue<PageAlertInstance> _pendingAlerts;
    private int _isDisposed;
    private int _isProcessing;

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
    }

    /// <summary>
    ///     Event fired when a new alert should be shown.
    /// </summary>
    public event Func<PageAlertInstance, Task>? OnAlert;

    /// <summary>
    ///     Event fired when all alerts should be cleared.
    /// </summary>
    public event Action? OnClear;

    /// <summary>
    ///     Shows a general alert with the specified parameters.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="type">Type of the alert.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, AlertError>> ShowAlertAsync(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = null,
        bool isPermanent = false)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        if (_pendingAlerts.Count >= MaxQueueSize)
        {
            _logger?.LogWarning("Alert queue is full. Dropping alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Alert queue is full"));
        }

        try
        {
            var alertId = $"alert-{Guid.NewGuid():N}";

            var alert = new PageAlertInstance
            {
                Id = alertId,
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Type = type,
                Duration = duration ?? GetDefaultDuration(type),
                IsPermanent = isPermanent
            };

            _pendingAlerts.Enqueue(alert);

            // Start processing alerts, but don't await it
            _ = TryProcessPendingAlertsAsync();

            return Result<Unit, AlertError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error creating alert: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Shows a success alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowSuccessAsync(string title, string message,
        int? duration = DefaultSuccessDuration)
    {
        return ShowAlertAsync(title, message, PageAlertType.Success, duration);
    }

    /// <summary>
    ///     Shows an error alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowErrorAsync(string title, string message,
        int? duration = DefaultErrorDuration)
    {
        return ShowAlertAsync(title, message, PageAlertType.Error, duration);
    }

    /// <summary>
    ///     Shows a warning alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowWarningAsync(string title, string message, bool isPermanent = false)
    {
        return ShowAlertAsync(
            title,
            message,
            PageAlertType.Warning,
            isPermanent ? null : DefaultWarningDuration,
            isPermanent
        );
    }

    /// <summary>
    ///     Shows an info alert.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Task<Result<Unit, AlertError>> ShowInfoAsync(string title, string message, bool isPermanent = false)
    {
        return ShowAlertAsync(
            title,
            message,
            PageAlertType.Info,
            isPermanent ? null : DefaultInfoDuration,
            isPermanent
        );
    }

    /// <summary>
    ///     Clears all displayed alerts.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, AlertError>> ClearAsync()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            var semaphoreAcquired = false;
            try
            {
                semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(2), _cts.Token);
                if (!semaphoreAcquired)
                {
                    _logger?.LogWarning("Timeout waiting for semaphore in ClearAsync");
                    return Result<Unit, AlertError>.Failure(
                        AlertError.CreateFailed("Timeout waiting to clear alerts"));
                }

                // Clear pending queue
                while (_pendingAlerts.TryDequeue(out _)) { }

                // Invoke clear event
                OnClear?.Invoke();

                return Result<Unit, AlertError>.Success(Unit.Value);
            }
            finally
            {
                if (semaphoreAcquired && !_cts.IsCancellationRequested)
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
                AlertError.CreateFailed($"Error clearing alerts: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Disposes the service and its resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        try
        {
            // Cancel any pending operations
            await _cts.CancelAsync();

            // Clear pending queue to prevent memory leaks
            while (_pendingAlerts.TryDequeue(out _)) { }

            // Dispose resources
            _cts.Dispose();
            _eventSemaphore.Dispose();

            // Clear event handlers to prevent memory leaks
            OnAlert = null;
            OnClear = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing PageAlertService");
        }
    }

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
                    while (_pendingAlerts.TryDequeue(out var alert))
                    {
                        try
                        {
                            await OnAlert.Invoke(alert);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error processing alert: {AlertId}", alert.Id);
                        }
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

    #region Backwards Compatibility Methods

    // These methods maintain backwards compatibility with the old sync API

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
}
