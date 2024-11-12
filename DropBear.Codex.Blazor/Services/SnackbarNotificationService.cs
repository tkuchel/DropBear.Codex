#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage snackbar notifications.
/// </summary>
public sealed class SnackbarNotificationService : ISnackbarNotificationService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SnackbarNotificationService>();
    private readonly object _eventLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarNotificationService" /> class.
    /// </summary>
    public SnackbarNotificationService()
    {
    }

    /// <summary>
    ///     Occurs when a new snackbar should be shown.
    /// </summary>
    public event AsyncEventHandler<SnackbarNotificationEventArgs>? OnShow;

    /// <summary>
    ///     Occurs when all snackbars should be hidden.
    /// </summary>
    public event AsyncEventHandler<EventArgs>? OnHideAll;

    /// <summary>
    ///     Shows a snackbar notification with the specified message and options.
    /// </summary>
    public Task<bool> ShowAsync(
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 1500,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        return ShowAsync("Notification", message, type, duration, isDismissible, actionText, onAction);
    }

    /// <summary>
    ///     Shows a snackbar notification with the specified title, message, and options.
    /// </summary>
    public async Task<bool> ShowAsync(
        string title,
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 1500,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        var options = new SnackbarNotificationOptions(
            title,
            message,
            type,
            duration,
            isDismissible,
            actionText,
            onAction);

        var result = await InvokeEventAsync(OnShow, new SnackbarNotificationEventArgs(options));
        if (result)
        {
            Logger.Debug("Snackbar notification shown with title '{Title}' and message '{Message}'.", title,
                message);
        }
        else
        {
            Logger.Warning("Snackbar notification could not be shown because there are no subscribers.");
        }

        return result;
    }

    /// <summary>
    ///     Hides all snackbar notifications.
    /// </summary>
    public async Task<bool> HideAllAsync()
    {
        var result = await InvokeEventAsync(OnHideAll, EventArgs.Empty);
        if (result)
        {
            Logger.Debug("All snackbar notifications hidden.");
        }
        else
        {
            Logger.Warning("No subscribers for OnHideAll event.");
        }

        return result;
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

        Delegate[] invocationList;
        lock (_eventLock)
        {
            invocationList = eventHandler.GetInvocationList();
        }

        var tasks = new List<Task>();
        foreach (var handler in invocationList)
        {
            var func = (AsyncEventHandler<TEventArgs>)handler;
            try
            {
                tasks.Add(func(this, args));
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

        try
        {
            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in event handlers.");
            return false;
        }
    }
}
