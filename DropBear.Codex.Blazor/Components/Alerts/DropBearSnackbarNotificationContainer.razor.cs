#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying snackbar notifications.
/// </summary>
public sealed partial class DropBearSnackbarNotificationContainer : DropBearComponentBase, IAsyncDisposable
{
    private const int MaxChannelSnackbars = 100;
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSnackbarNotificationContainer>();
    private readonly List<SnackbarInstance> _channelSnackbars = new();
    private readonly TimeSpan _debounceDuration = TimeSpan.FromMilliseconds(100);
    private readonly List<SnackbarInstance> _snackbars = new();
    private IDisposable? _channelSubscription;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<SnackbarInstance> CombinedSnackbars => _snackbars.Concat(_channelSnackbars);

    public async ValueTask DisposeAsync()
    {
        Logger.Debug("Disposing SnackbarNotificationContainer...");
        UnsubscribeFromSnackbarEvents();
        _channelSubscription?.Dispose();
        await DisposeSnackbarsAsync(_snackbars);
        await DisposeSnackbarsAsync(_channelSnackbars);
        Logger.Debug("SnackbarNotificationContainer disposed.");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToSnackbarEvents();
        SubscribeToChannelNotifications(ChannelId);
        Logger.Debug("SnackbarNotificationContainer initialized.");
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        Logger.Debug("Channel Id set to {ChannelId} for SnackbarContainer.", ChannelId);
    }

    private void SubscribeToSnackbarEvents()
    {
        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbarsAsync;
        Logger.Debug("Subscribed to SnackbarService events.");
    }

    private void UnsubscribeFromSnackbarEvents()
    {
        SnackbarService.OnShow -= ShowSnackbarAsync;
        SnackbarService.OnHideAll -= HideAllSnackbarsAsync;
        Logger.Debug("Unsubscribed from SnackbarService events.");
    }

    private void SubscribeToChannelNotifications(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            Logger.Warning("Channel ID is null or empty. Skipping channel subscription.");
            return;
        }

        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channelId, HandleNotification).AddTo(bag);
        _channelSubscription = bag.Build();
    }

    private ValueTask HandleNotification(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.Toast)
        {
            return ValueTask.CompletedTask;
        }

        var snackbarOptions = new SnackbarNotificationOptions(
            notification.Title ?? "Notification",
            notification.Message,
            MapSnackbarType(notification.Severity),
            2500);

        var snackbar = new SnackbarInstance(snackbarOptions);
        AddChannelSnackbar(snackbar);

        return ValueTask.CompletedTask;
    }

    private void AddChannelSnackbar(SnackbarInstance snackbar)
    {
        if (_channelSnackbars.Count >= MaxChannelSnackbars)
        {
            _channelSnackbars.RemoveAt(0);
        }

        _channelSnackbars.Add(snackbar);
        _ = DebouncedStateUpdateAsync();
    }

    private async Task ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        var snackbar = new SnackbarInstance(e.Options);
        _snackbars.Add(snackbar);
        Logger.Debug("Snackbar added with ID: {SnackbarId}", snackbar.Id);

        await DebouncedStateUpdateAsync();
        await Task.Delay(50); // Ensure DOM is updated before showing

        try
        {
            if (snackbar.ComponentRef is not null)
            {
                await snackbar.ComponentRef.ShowAsync();
                Logger.Debug("Snackbar shown with ID: {SnackbarId}", snackbar.Id);
            }

            if (snackbar.Duration > 0)
            {
                await Task.Delay(snackbar.Duration);
                await RemoveSnackbarAsync(snackbar);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while showing snackbar: {SnackbarId}", snackbar.Id);
        }
    }

    private async Task RemoveSnackbarAsync(SnackbarInstance snackbar)
    {
        if (_snackbars.Contains(snackbar))
        {
            _snackbars.Remove(snackbar);
        }
        else if (_channelSnackbars.Contains(snackbar))
        {
            _channelSnackbars.Remove(snackbar);
        }

        try
        {
            if (snackbar.ComponentRef is not null)
            {
                await snackbar.ComponentRef.DismissAsync();
                Logger.Debug("Snackbar dismissed with ID: {SnackbarId}", snackbar.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while removing snackbar: {SnackbarId}", snackbar.Id);
        }

        await DebouncedStateUpdateAsync();
    }

    private async Task HideAllSnackbarsAsync(object? sender, EventArgs e)
    {
        Logger.Debug("Hiding all snackbars...");
        try
        {
            await HideAllSnackbarsInternalAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while hiding all snackbars.");
        }
    }

    private async Task HideAllSnackbarsInternalAsync()
    {
        var tasks = CombinedSnackbars.Select(s => s.ComponentRef?.DismissAsync() ?? Task.CompletedTask);
        await Task.WhenAll(tasks);
        _snackbars.Clear();
        _channelSnackbars.Clear();
        Logger.Debug("All snackbars hidden and cleared.");
        await DebouncedStateUpdateAsync();
    }

    private async Task DebouncedStateUpdateAsync()
    {
        // await DebounceService.DebounceAsync(
        //     () => InvokeAsync(StateHasChanged),
        //     "SnackbarContainerStateUpdate",
        //     _debounceDuration
        // );
        await InvokeAsync(StateHasChanged);
    }

    private async Task DisposeSnackbarsAsync(List<SnackbarInstance> snackbars)
    {
        foreach (var snackbar in snackbars)
        {
            try
            {
                if (snackbar.ComponentRef is not null)
                {
                    await snackbar.ComponentRef.DisposeAsync();
                }
            }
            catch (JSDisconnectedException ex)
            {
                Logger.Warning(ex, "JSDisconnectedException occurred while disposing snackbar: {SnackbarId}",
                    snackbar.Id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error occurred while disposing snackbar: {SnackbarId}", snackbar.Id);
            }
        }
    }

    private async Task OnSnackbarAction(SnackbarInstance snackbar)
    {
        try
        {
            if (snackbar.OnAction is not null)
            {
                await snackbar.OnAction.Invoke();
                Logger.Debug("Snackbar action invoked for SnackbarId: {SnackbarId}", snackbar.Id);
            }

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling snackbar action: {SnackbarId}", snackbar.Id);
        }
    }

    /// <summary>
    ///     Maps NotificationSeverity to SnackbarType.
    /// </summary>
    private SnackbarType MapSnackbarType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => SnackbarType.Information,
            NotificationSeverity.Success => SnackbarType.Success,
            NotificationSeverity.Warning => SnackbarType.Warning,
            NotificationSeverity.Error => SnackbarType.Error,
            NotificationSeverity.Critical => SnackbarType.Error,
            NotificationSeverity.NotSpecified => SnackbarType.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown NotificationSeverity value")
        };
    }

    private sealed class SnackbarInstance : SnackbarNotificationOptions
    {
        public SnackbarInstance(SnackbarNotificationOptions options)
            : base(options.Title, options.Message, options.Type, options.Duration, options.IsDismissible,
                options.ActionText, options.OnAction)
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public DropBearSnackbarNotification? ComponentRef { get; set; }
    }
}
