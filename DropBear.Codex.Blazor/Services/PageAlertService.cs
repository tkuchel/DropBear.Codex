#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
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
    private readonly IAlertChannelManager _channelManager;
    private readonly SemaphoreSlim _eventLock = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PageAlertService" /> class.
    /// </summary>
    public PageAlertService()
    {
        _channelManager = new AlertChannelManager();
    }

    public PageAlertService(IAlertChannelManager channelManager)
    {
        _channelManager = channelManager;
    }

    public IEnumerable<PageAlert> Alerts => _alerts.Values;

    /// <summary>
    ///     Occurs when an alert should be added.
    /// </summary>
    public event AsyncEventHandler<PageAlertEventArgs>? OnAddAlert;

    /// <summary>
    ///     Occurs when an alert should be removed.
    /// </summary>
    public event AsyncEventHandler<PageAlertEventArgs>? OnRemoveAlert;

    /// <summary>
    ///     Occurs when all alerts should be cleared.
    /// </summary>
    public event AsyncEventHandler<EventArgs>? OnClearAlerts;

    /// <summary>
    ///     Adds an alert with the specified details.
    /// </summary>
    public async Task<bool> AddAlertAsync(string title, string message, AlertType type, bool isDismissible,
        string? channelId = null, int? durationMs = 5000)
    {
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

        Logger.Debug("Added new alert: {AlertTitle} - Type: {AlertType}, Dismissible: {IsDismissible}", title, type,
            isDismissible);
        var result = await InvokeEventAsync(OnAddAlert, new PageAlertEventArgs(alert));

        if (durationMs.HasValue)
        {
            _ = RemoveAlertAfterDelayAsync(alert.Id, durationMs.Value);
        }

        return result;
    }

    /// <summary>
    ///     Removes an alert by its ID.
    /// </summary>
    public async Task<bool> RemoveAlertAsync(Guid id)
    {
        if (_alerts.TryRemove(id, out var alert))
        {
            Logger.Debug("Alert with ID {AlertId} removed successfully.", id);
            return await InvokeEventAsync(OnRemoveAlert, new PageAlertEventArgs(alert));
        }

        Logger.Warning("Attempted to remove non-existing alert with ID: {AlertId}", id);
        return false;
    }

    /// <summary>
    ///     Clears all alerts.
    /// </summary>
    public async Task<bool> ClearAlertsAsync()
    {
        _alerts.Clear();
        Logger.Debug("All alerts cleared successfully.");
        return await InvokeEventAsync(OnClearAlerts, EventArgs.Empty);
    }

    private async Task RemoveAlertAfterDelayAsync(Guid id, int delayMs)
    {
        await Task.Delay(delayMs);
        await RemoveAlertAsync(id);
    }

    /// <summary>
    ///     Helper method to invoke events asynchronously and handle exceptions.
    /// </summary>
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

            if (handlers.Length == 0) return false;

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

            if (tasks.Count == 0) return false;

            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in event handlers.");
            return false;
        }
        finally
        {
            _eventLock.Release();
        }
    }
}

/// <summary>
///     Represents the event arguments for page alert events.
/// </summary>
public class PageAlertEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PageAlertEventArgs" /> class.
    /// </summary>
    /// <param name="alert">The page alert associated with the event.</param>
    public PageAlertEventArgs(PageAlert alert)
    {
        Alert = alert;
    }

    /// <summary>
    ///     Gets the page alert associated with the event.
    /// </summary>
    public PageAlert Alert { get; }
}
