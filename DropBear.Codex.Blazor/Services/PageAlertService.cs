#region

using System.Collections.Frozen;
using System.Threading.Channels;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
/// High-performance page alert service optimized for .NET 9+ and Blazor Server.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    #region Constants and Static Data

    private const int MaxQueueSize = 100;
    private const int OperationTimeoutMs = 5000;

    // Use FrozenDictionary for better performance in .NET 9+
    private static readonly FrozenDictionary<PageAlertType, int> DefaultDurations =
        new Dictionary<PageAlertType, int>
        {
            [PageAlertType.Success] = 5000,
            [PageAlertType.Error] = 8000,
            [PageAlertType.Warning] = 6000,
            [PageAlertType.Info] = 5000
        }.ToFrozenDictionary();

    #endregion

    #region Fields

    private readonly ILogger<PageAlertService>? _logger;
    private readonly Channel<PageAlertInstance> _alertChannel;
    private readonly ChannelWriter<PageAlertInstance> _writer;
    private readonly ChannelReader<PageAlertInstance> _reader;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _eventSemaphore = new(1, 1);

    private Task? _processingTask;
    private volatile bool _isDisposed;

    #endregion

    #region Events

    public event Func<PageAlertInstance, Task>? OnAlert;
    public event Action? OnClear;

    #endregion

    #region Constructor

    public PageAlertService(ILogger<PageAlertService>? logger = null)
    {
        _logger = logger;

        // Use bounded channel for better memory management
        var options = new BoundedChannelOptions(MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _alertChannel = Channel.CreateBounded<PageAlertInstance>(options);
        _writer = _alertChannel.Writer;
        _reader = _alertChannel.Reader;

        // Start background processing
        _processingTask = ProcessAlertsAsync(_cts.Token);

        _logger?.LogDebug("PageAlertService initialized with channel-based processing");
    }

    #endregion

    #region Public Async Methods

    public async Task<Result<Unit, AlertError>> ShowAlertAsync(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = null,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token, cancellationToken);
            linkedCts.CancelAfter(OperationTimeoutMs);

            var alert = new PageAlertInstance
            {
                Id = $"alert-{Guid.NewGuid():N}",
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Type = type,
                Duration = duration ?? DefaultDurations[type],
                IsPermanent = isPermanent,
                CreatedAt = DateTime.UtcNow
            };

            // Try to write to channel
            if (!await _writer.WaitToWriteAsync(linkedCts.Token))
            {
                return Result<Unit, AlertError>.Failure(
                    AlertError.CreateFailed("Alert channel is closed"));
            }

            if (!_writer.TryWrite(alert))
            {
                return Result<Unit, AlertError>.Failure(
                    AlertError.CreateFailed("Alert queue is full"));
            }

            return Result<Unit, AlertError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("ShowAlert operation cancelled for: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating alert: {Title}", title);
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error creating alert: {ex.Message}"), ex);
        }
    }

    public Task<Result<Unit, AlertError>> ShowSuccessAsync(
        string title,
        string message,
        int? duration = 5000,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Success,
            duration ?? DefaultDurations[PageAlertType.Success], false, cancellationToken);
    }

    public Task<Result<Unit, AlertError>> ShowErrorAsync(
        string title,
        string message,
        int? duration = 8000,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Error,
            duration ?? DefaultDurations[PageAlertType.Error], false, cancellationToken);
    }

    public Task<Result<Unit, AlertError>> ShowWarningAsync(
        string title,
        string message,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Warning,
            isPermanent ? null : DefaultDurations[PageAlertType.Warning], isPermanent, cancellationToken);
    }

    public Task<Result<Unit, AlertError>> ShowInfoAsync(
        string title,
        string message,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        return ShowAlertAsync(title, message, PageAlertType.Info,
            isPermanent ? null : DefaultDurations[PageAlertType.Info], isPermanent, cancellationToken);
    }

    public async Task<Result<Unit, AlertError>> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("PageAlertService is disposed"));
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token, cancellationToken);
            linkedCts.CancelAfter(OperationTimeoutMs);

            var semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(2), linkedCts.Token);
            if (!semaphoreAcquired)
            {
                return Result<Unit, AlertError>.Failure(
                    AlertError.CreateFailed("Timeout waiting to acquire semaphore"));
            }

            try
            {
                // Clear any pending alerts in channel by completing and recreating
                _writer.Complete();

                // Drain the channel
                await foreach (var _ in _reader.ReadAllAsync(linkedCts.Token))
                {
                    // Just drain, don't process
                }

                OnClear?.Invoke();
                _logger?.LogDebug("Cleared all pending alerts");

                return Result<Unit, AlertError>.Success(Unit.Value);
            }
            finally
            {
                _eventSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing alerts");
            return Result<Unit, AlertError>.Failure(
                AlertError.CreateFailed($"Error clearing alerts: {ex.Message}"), ex);
        }
    }

    #endregion

    #region Compatibility Sync Methods

    public void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false)
    {
        _ = ShowAlertAsync(title, message, type, duration, isPermanent)
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void ShowSuccess(string title, string message, int? duration = 5000)
    {
        _ = ShowSuccessAsync(title, message, duration)
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void ShowError(string title, string message, int? duration = 8000)
    {
        _ = ShowErrorAsync(title, message, duration)
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void ShowWarning(string title, string message, bool isPermanent = false)
    {
        _ = ShowWarningAsync(title, message, isPermanent)
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void ShowInfo(string title, string message, bool isPermanent = false)
    {
        _ = ShowInfoAsync(title, message, isPermanent)
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Clear()
    {
        _ = ClearAsync()
            .ContinueWith(LogTaskErrors, TaskContinuationOptions.OnlyOnFaulted);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Background task for processing alerts using modern async enumerable.
    /// </summary>
    private async Task ProcessAlertsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var alert in _reader.ReadAllAsync(cancellationToken))
            {
                await ProcessSingleAlertAsync(alert);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in alert processing background task");
        }
    }

    /// <summary>
    /// Process a single alert with error handling.
    /// </summary>
    private async Task ProcessSingleAlertAsync(PageAlertInstance alert)
    {
        if (OnAlert == null) return;

        try
        {
            var semaphoreAcquired = await _eventSemaphore.WaitAsync(TimeSpan.FromSeconds(1), _cts.Token);
            if (!semaphoreAcquired)
            {
                _logger?.LogWarning("Timeout waiting for semaphore when processing alert: {AlertId}", alert.Id);
                return;
            }

            try
            {
                await OnAlert.Invoke(alert);
                _logger?.LogDebug("Alert processed: {AlertId} - {Title}", alert.Id, alert.Title);
            }
            finally
            {
                _eventSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing alert: {AlertId}", alert.Id);
        }
    }

    /// <summary>
    /// Log task errors for fire-and-forget operations.
    /// </summary>
    private void LogTaskErrors(Task task)
    {
        if (task.Exception != null)
        {
            _logger?.LogError(task.Exception, "Error in fire-and-forget alert operation");
        }
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _logger?.LogDebug("Disposing PageAlertService");

            // Complete the channel to stop accepting new alerts
            _writer.Complete();

            // Cancel background operations
            await _cts.CancelAsync();

            // Wait for processing to complete
            if (_processingTask != null)
            {
                try
                {
                    await _processingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Dispose resources
            _cts.Dispose();
            _eventSemaphore.Dispose();

            // Clear event handlers
            OnAlert = null;
            OnClear = null;

            _logger?.LogDebug("PageAlertService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing PageAlertService");
        }
    }

    #endregion
}
