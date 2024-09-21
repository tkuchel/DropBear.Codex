#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications;
using DropBear.Codex.Notifications.Enums;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;
using AlertType = DropBear.Codex.Notifications.Enums.AlertType;

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
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(100);
    private readonly List<SnackbarInstance> _snackbars = new();
    private IDisposable? _disposable;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<SnackbarInstance> CombinedSnackbars => _snackbars.Concat(_channelSnackbars);

    public async ValueTask DisposeAsync()
    {
        Logger.Debug("Disposing SnackbarNotificationContainer...");
        UnsubscribeSnackbarServiceEvents();
        _disposable?.Dispose();
        await DisposeSnackbarsAsync(_snackbars);
        await DisposeSnackbarsAsync(_channelSnackbars);
        Logger.Debug("SnackbarNotificationContainer disposed.");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeSnackbarServiceEvents();
        SubscribeToChannelNotifications(ChannelId);
        Logger.Debug("SnackbarNotificationContainer initialized.");
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

    private void SubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbars;
        Logger.Debug("Subscribed to SnackbarService events.");
    }

    private void UnsubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow -= ShowSnackbarAsync;
        SnackbarService.OnHideAll -= HideAllSnackbars;
        Logger.Debug("Unsubscribed from SnackbarService events.");
    }

    private void SubscribeToChannelNotifications(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            Logger.Warning("Channel ID is null or empty. Skipping channel subscription.");
            return;
        }

        if (Serializer == null)
        {
            Logger.Error("Serializer is not available. Cannot subscribe to channel notifications.");
            return;
        }

        var bag = DisposableBag.CreateBuilder();
        ChannelNotificationSubscriber.Subscribe(channelId, Handler).AddTo(bag);
        _disposable = bag.Build();

        async void Handler(byte[] message)
        {
            try
            {
                var notification = await Serializer.DeserializeAsync<Notification>(message);

                if (notification.Type != AlertType.Snackbar)
                {
                    return;
                }

                Logger.Information("Received snackbar notification for channel {ChannelId}: {Message}", channelId,
                    notification.Message);
                var snackbarOptions = new SnackbarNotificationOptions(
                    "New Notification",
                    notification.Message,
                    MapSnackbarType(notification.Severity),
                    3000 // IsDismissible
                );
                await InvokeAsync(() => AddChannelSnackbar(new SnackbarInstance(snackbarOptions)));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing notification for channel {ChannelId}", channelId);
            }
        }
    }

    private async Task ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        var snackbar = new SnackbarInstance(e.Options);
        _snackbars.Add(snackbar);
        Logger.Debug("Snackbar added with ID: {SnackbarId}", snackbar.Id);

        await DebouncedStateHasChanged();
        await Task.Delay(50); // Small delay to ensure the DOM is updated

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
            throw;
        }
    }

    private void AddChannelSnackbar(SnackbarInstance snackbar)
    {
        _channelSnackbars.Add(snackbar);
        if (_channelSnackbars.Count > MaxChannelSnackbars)
        {
            _channelSnackbars.RemoveAt(0);
        }

        _ = DebouncedStateHasChanged();
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
            throw;
        }

        await DebouncedStateHasChanged();
    }

    private async Task HideAllSnackbars(object? sender, EventArgs e)
    {
        Logger.Debug("Hiding all snackbars...");

        try
        {
            await HideAllSnackbarsInternalAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while hiding all snackbars.");
            throw;
        }
    }

    private async Task HideAllSnackbarsInternalAsync()
    {
        var tasks = CombinedSnackbars.Select(s => s.ComponentRef?.DismissAsync() ?? Task.CompletedTask);
        await Task.WhenAll(tasks);
        _snackbars.Clear();
        _channelSnackbars.Clear();

        Logger.Debug("All snackbars hidden and cleared.");
        await DebouncedStateHasChanged();
    }

    private async Task OnSnackbarAction(SnackbarInstance snackbar)
    {
        try
        {
            await snackbar.OnAction.Invoke();
            Logger.Debug("Snackbar action invoked for SnackbarId: {SnackbarId}", snackbar.Id);

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling snackbar action: {SnackbarId}", snackbar.Id);
            throw;
        }
    }

    private async Task DebouncedStateHasChanged()
    {
        await DebounceService.DebounceAsync(
            () => InvokeAsync(StateHasChanged),
            "SnackbarContainerStateUpdate",
            _debounceTime
        );
    }

    private SnackbarType MapSnackbarType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => SnackbarType.Information,
            NotificationSeverity.Warning => SnackbarType.Warning,
            NotificationSeverity.Error => SnackbarType.Error,
            NotificationSeverity.Critical => SnackbarType.Error,
            _ => SnackbarType.Information
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
