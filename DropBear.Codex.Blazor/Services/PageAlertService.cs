#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Events;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage page alerts.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<PageAlertService>();
    private readonly ConcurrentDictionary<Guid, PageAlert> _alerts = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _autoRemovalTokens = new();
    private readonly IAlertChannelManager _channelManager;
    private readonly SemaphoreSlim _eventLock = new(1, 1);
    private volatile bool _isDisposed;

    public PageAlertService(IAlertChannelManager? channelManager = null)
    {
        _channelManager = channelManager ?? new AlertChannelManager();
    }

    public IEnumerable<PageAlert> Alerts => _alerts.Values;
    public event AsyncEventHandler<EventArgs>? OnClearAlerts;

    public async Task<bool> AddAlertAsync(
        string title,
        string message,
        AlertType type,
        bool isDismissible,
        string? channelId = null,
        int? durationMs = 5000)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PageAlertService));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        if (channelId != null && !_channelManager.IsValidChannel(channelId))
        {
            Logger.Warning("Attempted to add alert to invalid channel: {ChannelId}", channelId);
            return false;
        }

        var alert = new PageAlert(title, message, type, isDismissible, channelId);

        if (!_alerts.TryAdd(alert.Id, alert))
        {
            Logger.Error("Failed to add alert with ID {AlertId}.", alert.Id);
            return false;
        }

        Logger.Debug("Added new alert: {AlertTitle} - Type: {AlertType}, Dismissible: {IsDismissible}",
            title, type, isDismissible);

        var result = await InvokeEventAsync(OnAddAlert, new PageAlertEventArgs(alert));

        if (durationMs.HasValue && durationMs.Value > 0)
        {
            var cts = new CancellationTokenSource();
            _autoRemovalTokens.TryAdd(alert.Id, cts);
            _ = AutoRemoveAlertAsync(alert.Id, durationMs.Value, cts.Token);
        }

        return result;
    }

    public async Task<bool> RemoveAlertAsync(Guid id)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PageAlertService));
        }

        if (_autoRemovalTokens.TryRemove(id, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        if (_alerts.TryRemove(id, out var alert))
        {
            Logger.Debug("Alert with ID {AlertId} removed successfully.", id);
            return await InvokeEventAsync(OnRemoveAlert, new PageAlertEventArgs(alert));
        }

        Logger.Warning("Attempted to remove non-existing alert with ID: {AlertId}", id);
        return false;
    }

    public async Task<bool> ClearAlertsAsync()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PageAlertService));
        }

        // Cancel all auto-removal timers
        foreach (var (id, cts) in _autoRemovalTokens)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _autoRemovalTokens.TryRemove(id, out _);
        }

        _alerts.Clear();
        Logger.Debug("All alerts cleared successfully.");
        return await InvokeEventAsync(OnClearAlerts, EventArgs.Empty);
    }

    public event AsyncEventHandler<PageAlertEventArgs>? OnAddAlert;
    public event AsyncEventHandler<PageAlertEventArgs>? OnRemoveAlert;

    private async Task AutoRemoveAlertAsync(Guid alertId, int delayMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await RemoveAlertAsync(alertId);
            }
        }
        catch (OperationCanceledException)
        {
            // Alert was manually removed or service was disposed
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in auto-removal for alert {AlertId}", alertId);
        }
    }

    private async Task<bool> InvokeEventAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? eventHandler, TEventArgs args)
    {
        if (eventHandler == null)
        {
            Logger.Warning("No subscribers for event.");
            return false;
        }

        await _eventLock.WaitAsync();
        try
        {
            var handlers = eventHandler.GetInvocationList()
                .Cast<AsyncEventHandler<TEventArgs>>()
                .ToArray();

            if (handlers.Length == 0)
            {
                return false;
            }

            var tasks = new List<Task>();
            foreach (var handler in handlers)
            {
                try
                {
                    tasks.Add(handler(this, args));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error invoking event handler.");
                }
            }

            if (tasks.Count == 0)
            {
                return false;
            }

            await Task.WhenAll(tasks);
            return true;
        }
        finally
        {
            _eventLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        foreach (var (_, cts) in _autoRemovalTokens)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        _autoRemovalTokens.Clear();

        _alerts.Clear();
        _eventLock.Dispose();

        Logger.Information("PageAlertService disposed successfully");
    }
}
