#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearSnackbarNotificationContainer : DropBearComponentBase
{
    private const int MaxChannelSnackbars = 100;
    private const int StateUpdateDebounceMs = 100;
    private const int DomUpdateDelayMs = 50;
    private readonly string _containerId;

    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private ConcurrentQueue<SnackbarInstance> _activeSnackbars = new();
    private IDisposable? _channelSubscription;
    private bool _isSubscribed;

    public DropBearSnackbarNotificationContainer()
    {
        _containerId = $"snackbar-container-{ComponentId}";
    }

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitializeSubscriptionsAsync();
    }

    private async Task InitializeSubscriptionsAsync()
    {
        try
        {
            SubscribeToSnackbarEvents();
            if (!string.IsNullOrEmpty(ChannelId))
            {
                await SubscribeToChannelNotificationsAsync();
            }

            Logger.Debug("SnackbarNotificationContainer initialized");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize subscriptions");
            throw;
        }
    }

    private void SubscribeToSnackbarEvents()
    {
        if (_isSubscribed)
        {
            return;
        }

        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbarsAsync;
        _isSubscribed = true;
        Logger.Debug("Subscribed to SnackbarService events");
    }

    private async Task SubscribeToChannelNotificationsAsync()
    {
        var bag = DisposableBag.CreateBuilder();
        await InvokeAsync(() =>
        {
            NotificationSubscriber.Subscribe<string, Notification>(
                ChannelId,
                HandleNotificationAsync
            ).AddTo(bag);
        });
        _channelSubscription = bag.Build();
        Logger.Debug("Subscribed to channel notifications for {ChannelId}", ChannelId);
    }

    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (IsDisposed || notification.Type != NotificationType.Toast)
        {
            return;
        }

        try
        {
            var snackbarOptions = new SnackbarNotificationOptions(
                notification.Title ?? "Notification",
                notification.Message,
                MapSnackbarType(notification.Severity));

            await ShowSnackbarAsync(this, new SnackbarNotificationEventArgs(snackbarOptions));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification");
        }
    }

    private async Task ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            var snackbar = new SnackbarInstance(e.Options);
            await ManageSnackbarQueueAsync(snackbar);
            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task ManageSnackbarQueueAsync(SnackbarInstance snackbar)
    {
        while (_activeSnackbars.Count >= MaxChannelSnackbars)
        {
            if (_activeSnackbars.TryDequeue(out var oldSnackbar))
            {
                await RemoveSnackbarAsync(oldSnackbar);
            }
        }

        _activeSnackbars.Enqueue(snackbar);
        Logger.Debug("Snackbar added: {Id}", snackbar.Id);

        if (snackbar.Duration > 0)
        {
            _ = AutoHideSnackbarAsync(snackbar);
        }
    }

    private async Task RemoveSnackbarAsync(SnackbarInstance snackbar)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (snackbar.ComponentRef != null)
            {
                await snackbar.ComponentRef.DismissAsync();
            }

            _activeSnackbars = new ConcurrentQueue<SnackbarInstance>(
                _activeSnackbars.Where(s => s.Id != snackbar.Id)
            );

            await SnackbarService.RemoveAsync(snackbar.Id);
            await DebouncedStateUpdateAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing snackbar: {Id}", snackbar.Id);
        }
    }

    private async Task AutoHideSnackbarAsync(SnackbarInstance snackbar)
    {
        try
        {
            await Task.Delay(snackbar.Duration);
            if (!IsDisposed)
            {
                await RemoveSnackbarAsync(snackbar);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error auto-hiding snackbar: {Id}", snackbar.Id);
        }
    }

    private async Task HideAllSnackbarsAsync(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            var tasks = _activeSnackbars
                .Where(s => s.ComponentRef != null)
                .Select(s => s.ComponentRef!.DismissAsync());

            await Task.WhenAll(tasks);
            _activeSnackbars.Clear();
            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task DebouncedStateUpdateAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await DebounceService.DebounceAsync(
                async () =>
                {
                    if (!IsDisposed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                },
                "SnackbarContainerStateUpdate",
                TimeSpan.FromMilliseconds(StateUpdateDebounceMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in debounced state update");
        }
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await SafeJsVoidInteropAsync("snackbarContainer.cleanup", _containerId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during snackbar container cleanup: {ContainerId}", _containerId);
        }
        finally
        {
            UnsubscribeFromEvents();
            _updateLock.Dispose();
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (!_isSubscribed)
        {
            return;
        }

        SnackbarService.OnShow -= ShowSnackbarAsync;
        SnackbarService.OnHideAll -= HideAllSnackbarsAsync;
        _channelSubscription?.Dispose();
        _channelSubscription = null;
        _isSubscribed = false;

        Logger.Debug("Unsubscribed from all events");
    }

    private static SnackbarType MapSnackbarType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => SnackbarType.Information,
            NotificationSeverity.Success => SnackbarType.Success,
            NotificationSeverity.Warning => SnackbarType.Warning,
            NotificationSeverity.Error or NotificationSeverity.Critical => SnackbarType.Error,
            NotificationSeverity.NotSpecified => SnackbarType.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(severity))
        };
    }

    private string GetSnackbarClass(SnackbarInstance snackbar)
    {
        return $"snackbar-item snackbar-{snackbar.Type.ToString().ToLowerInvariant()}";
    }

    private async Task OnSnackbarActionAsync(SnackbarInstance snackbar)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (snackbar.OnAction is not null)
            {
                await snackbar.OnAction.Invoke();
                Logger.Debug("Snackbar action executed: {Id}", snackbar.Id);
            }

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling snackbar action: {Id}", snackbar.Id);
            throw;
        }
    }
}
