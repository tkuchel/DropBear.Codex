#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Alerts;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public sealed class SnackbarNotificationService : ISnackbarNotificationService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, SnackbarInstance> _activeSnackbars = new();
    private readonly SemaphoreSlim _eventLock = new(1, 1);
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<SnackbarNotificationService>();
    private bool _isDisposed;

    public IEnumerable<SnackbarInstance> ActiveSnackbars => _activeSnackbars.Values;

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        await HideAllAsync();
        _eventLock.Dispose();
        _activeSnackbars.Clear();
    }

    public event AsyncEventHandler<SnackbarNotificationEventArgs>? OnShow;
    public event AsyncEventHandler<EventArgs>? OnHideAll;

    public async Task<bool> ShowAsync(
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 1500,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        return await ShowAsync("Notification", message, type, duration, isDismissible, actionText, onAction);
    }

    public async Task<bool> ShowAsync(
        string title,
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 1500,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SnackbarNotificationService));
        }

        _logger.Debug(
            "Attempting to show snackbar - Title: {Title}, Message: {Message}, Type: {Type}",
            title, message, type);

        var options = new SnackbarNotificationOptions(
            title,
            message,
            type,
            duration,
            isDismissible,
            actionText,
            onAction);

        var snackbar = new SnackbarInstance(options);
        if (_activeSnackbars.TryAdd(snackbar.Id, snackbar))
        {
            _logger.Debug("Added snackbar to active snackbars: {Id}", snackbar.Id);
        }

        var result = await InvokeEventAsync(OnShow, new SnackbarNotificationEventArgs(options));
        if (result)
        {
            _logger.Debug(
                "Snackbar notification shown successfully - Id: {Id}, Title: {Title}",
                snackbar.Id, title);
        }
        else
        {
            _logger.Warning("No subscribers for snackbar notification");
            _activeSnackbars.TryRemove(snackbar.Id, out _);
        }

        return result;
    }

    public async Task<bool> HideAllAsync()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SnackbarNotificationService));
        }

        var result = await InvokeEventAsync(OnHideAll, EventArgs.Empty);
        if (result)
        {
            _activeSnackbars.Clear();
            _logger.Debug("All snackbar notifications hidden.");
        }
        else
        {
            _logger.Warning("No subscribers for OnHideAll event.");
        }

        return result;
    }

    public async Task<bool> RemoveAsync(Guid snackbarId)
    {
        if (!_activeSnackbars.TryRemove(snackbarId, out _))
        {
            return false;
        }

        _logger.Debug("Snackbar {Id} removed", snackbarId);
        return true;
    }

    private async Task<bool> InvokeEventAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? eventHandler, TEventArgs args)
    {
        if (eventHandler == null)
        {
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
                    _logger.Error(ex, "Error invoking event handler.");
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
}
