#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing page alerts with batching and rate limiting support.
///     Optimized for Blazor Server environments.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    private const int DefaultSuccessDuration = 5000;
    private const int DefaultErrorDuration = 8000;
    private const int DefaultWarningDuration = 6000;
    private const int DefaultInfoDuration = 5000;
    private const int MaxQueueSize = 100;
    private readonly CancellationTokenSource _cts;
    private readonly SemaphoreSlim _eventSemaphore;

    private readonly ILogger<PageAlertService>? _logger;
    private readonly ConcurrentQueue<PageAlertInstance> _pendingAlerts;
    private bool _isDisposed;

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

    public event Func<PageAlertInstance, Task>? OnAlert;
    public event Action? OnClear;

    /// <summary>
    ///     Shows a general alert with the specified parameters.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="type">Type of the alert.</param>
    /// <param name="duration">Duration in milliseconds (null for default).</param>
    /// <param name="isPermanent">If true, the alert will not auto-dismiss.</param>
    /// <exception cref="InvalidOperationException">Thrown if the service is disposed.</exception>
    public async Task ShowAlertAsync(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = null,
        bool isPermanent = false)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PageAlertService));
        }

        if (_pendingAlerts.Count >= MaxQueueSize)
        {
            _logger?.LogWarning("Alert queue is full. Dropping alert: {Title}", title);
            return;
        }

        var alert = new PageAlertInstance
        {
            Id = $"alert-{Guid.NewGuid():N}",
            Title = title,
            Message = message,
            Type = type,
            Duration = duration ?? GetDefaultDuration(type),
            IsPermanent = isPermanent
        };

        _pendingAlerts.Enqueue(alert);
        await TryProcessPendingAlertsAsync();
    }

    /// <summary>
    ///     Shows a success alert.
    /// </summary>
    public Task ShowSuccessAsync(string title, string message, int? duration = DefaultSuccessDuration)
    {
        return ShowAlertAsync(title, message, PageAlertType.Success, duration);
    }

    /// <summary>
    ///     Shows an error alert.
    /// </summary>
    public Task ShowErrorAsync(string title, string message, int? duration = DefaultErrorDuration)
    {
        return ShowAlertAsync(title, message, PageAlertType.Error, duration);
    }

    /// <summary>
    ///     Shows a warning alert.
    /// </summary>
    public Task ShowWarningAsync(string title, string message, bool isPermanent = false)
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
    public Task ShowInfoAsync(string title, string message, bool isPermanent = false)
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
    public async Task ClearAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _eventSemaphore.WaitAsync(_cts.Token);

            while (_pendingAlerts.TryDequeue(out _)) { }

            OnClear?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing alerts");
        }
        finally
        {
            if (!_cts.Token.IsCancellationRequested)
            {
                _eventSemaphore.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _isDisposed = true;
            await _cts.CancelAsync();
            _cts.Dispose();
            _eventSemaphore.Dispose();

            while (_pendingAlerts.TryDequeue(out _)) { }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing PageAlertService");
        }
    }

    private async Task TryProcessPendingAlertsAsync()
    {
        if (_isDisposed || OnAlert == null)
        {
            return;
        }

        try
        {
            await _eventSemaphore.WaitAsync(_cts.Token);

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
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing pending alerts");
        }
        finally
        {
            if (!_cts.Token.IsCancellationRequested)
            {
                _eventSemaphore.Release();
            }
        }
    }

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
    public void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false)
    {
        _ = ShowAlertAsync(title, message, type, duration, isPermanent);
    }

    public void ShowSuccess(string title, string message, int? duration = DefaultSuccessDuration)
    {
        _ = ShowSuccessAsync(title, message, duration);
    }

    public void ShowError(string title, string message, int? duration = DefaultErrorDuration)
    {
        _ = ShowErrorAsync(title, message, duration);
    }

    public void ShowWarning(string title, string message, bool isPermanent = false)
    {
        _ = ShowWarningAsync(title, message, isPermanent);
    }

    public void ShowInfo(string title, string message, bool isPermanent = false)
    {
        _ = ShowInfoAsync(title, message, isPermanent);
    }

    public void Clear()
    {
        _ = ClearAsync();
    }

    #endregion
}
