#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public sealed partial class DropBearSnackbarContainer : DropBearComponentBase
{
    private const int MaxSnackbars = 5;
    private readonly List<SnackbarInstance> _activeSnackbars = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IDisposable? _channelSubscription;
    [Parameter] public string? ChannelId { get; set; }

    private string GetPositionClass()
    {
        return "bottom-right";
        // Customize if needed
    }


    protected override void OnInitialized()
    {
        try
        {
            SubscribeToSnackbarEvents();
            if (!string.IsNullOrEmpty(ChannelId))
            {
                SubscribeToChannelNotifications();
            }

            SubscribeToGlobalNotifications();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize SnackbarContainer");
            throw;
        }
    }


    private void SubscribeToSnackbarEvents()
    {
        try
        {
            if (SnackbarService is null)
            {
                Logger.Warning("SnackbarService is null during event subscription");
                return;
            }

            SnackbarService.OnShow += ShowSnackbar;
            Logger.Debug("Subscribed to SnackbarService events");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error subscribing to SnackbarService events");
            throw;
        }
    }

    private void UnsubscribeFromSnackbarEvents()
    {
        try
        {
            if (SnackbarService is not null)
            {
                SnackbarService.OnShow -= ShowSnackbar;
                Logger.Debug("Unsubscribed from SnackbarService events");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error unsubscribing from SnackbarService events");
        }
    }

    private void SubscribeToChannelNotifications()
    {
        if (string.IsNullOrEmpty(ChannelId))
        {
            return;
        }

        var channel = $"{GlobalConstants.UserNotificationChannel}.{ChannelId}";
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channel, HandleNotificationAsync).AddTo(bag);
        _channelSubscription = bag.Build();

        Logger.Debug("Subscribed to channel notifications for {ChannelId}", ChannelId);
    }

    private void SubscribeToGlobalNotifications()
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync).AddTo(bag);
        _channelSubscription = bag.Build();

        Logger.Debug("Subscribed to global notifications");
    }

    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.Toast)
        {
            return;
        }

        try
        {
            var snackbar = new SnackbarInstance
            {
                Title = notification.Title ?? "Notification",
                Message = notification.Message,
                Type = MapNotificationSeverityToSnackbarType(notification.Severity),
                Duration = GetDurationForSeverity(notification.Severity)
            };

            await ShowSnackbar(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification");
        }
    }

    private static int GetDurationForSeverity(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => 5000,
            NotificationSeverity.Information => 5000,
            NotificationSeverity.Warning => 8000,
            NotificationSeverity.Error => 0, // Requires manual dismissal
            NotificationSeverity.Critical => 0, // Requires manual dismissal
            _ => 5000
        };
    }

    private static SnackbarType MapNotificationSeverityToSnackbarType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => SnackbarType.Success,
            NotificationSeverity.Information => SnackbarType.Information,
            NotificationSeverity.Warning => SnackbarType.Warning,
            NotificationSeverity.Error => SnackbarType.Error,
            NotificationSeverity.Critical => SnackbarType.Error,
            _ => SnackbarType.Information
        };
    }

    private async Task ShowSnackbar(SnackbarInstance snackbar)
    {
        try
        {
            await _semaphore.WaitAsync();

            while (_activeSnackbars.Count >= MaxSnackbars)
            {
                var oldestId = _activeSnackbars[0].Id;
                await RemoveSnackbar(oldestId);
            }

            _activeSnackbars.Add(snackbar);
            await InvokeAsync(StateHasChanged);

            try
            {
                // Allow render to complete
                await Task.Delay(50);
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", snackbar.Id);

                if (snackbar is { RequiresManualClose: false, Duration: > 0 })
                {
                    await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress",
                        snackbar.Id, snackbar.Duration);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing snackbar: {Id}", snackbar.Id);
                // Clean up on failure
                _activeSnackbars.Remove(snackbar);
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task AutoHideSnackbarAsync(SnackbarInstance snackbar)
    {
        try
        {
            await Task.Delay(snackbar.Duration);
            await RemoveSnackbar(snackbar.Id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error auto-hiding snackbar: {SnackbarId}", snackbar.Id);
        }
    }

    private async Task RemoveSnackbar(string id)
    {
        try
        {
            await _semaphore.WaitAsync();

            var snackbar = _activeSnackbars.FirstOrDefault(s => s.Id == id);
            if (snackbar != null)
            {
                try
                {
                    // First handle the JS side
                    await SafeJsVoidInteropAsync("DropBearSnackbar.hide", id);

                    // Wait for animation
                    await Task.Delay(300);

                    // Then update the state
                    _activeSnackbars.Remove(snackbar);
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error removing snackbar: {Id}", id);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeJsVoidInteropAsync("DropBearSnackbar.initialize", ComponentId);
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                UnsubscribeFromSnackbarEvents();
                _channelSubscription?.Dispose();

                // Dispose snackbars one by one with a delay
                foreach (var snackbar in _activeSnackbars.ToList())
                {
                    try
                    {
                        await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", snackbar.Id);
                        await Task.Delay(50); // Small delay between disposals
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error disposing snackbar: {Id}", snackbar.Id);
                    }
                }

                _activeSnackbars.Clear();
                _semaphore.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during snackbar container disposal");
            }
        }

        await base.DisposeAsync(disposing);
    }
}
