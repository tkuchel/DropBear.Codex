#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public sealed partial class DropBearSnackbarContainer : DropBearComponentBase
{
    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private readonly List<IDisposable> _subscriptions = new();
    private bool _isDisposed;
    [Inject] private ISnackbarService SnackbarService { get; set; } = null!;
    [Inject] private IAsyncSubscriber<string, Notification> NotificationSubscriber { get; set; } = null!;
    [Inject] private new ILogger<DropBearSnackbarContainer> Logger { get; set; } = null!;

    [Parameter] public string? ChannelId { get; set; }
    [Parameter] public SnackbarPosition Position { get; set; } = SnackbarPosition.BottomRight;

    private string PositionClass => Position switch
    {
        SnackbarPosition.TopLeft => "top-left",
        SnackbarPosition.TopRight => "top-right",
        SnackbarPosition.BottomLeft => "bottom-left",
        _ => "bottom-right"
    };

    protected override void OnInitialized()
    {
        try
        {
            SubscribeToEvents();
            base.OnInitialized();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize DropBearSnackbarContainer");
            throw;
        }
    }

    private void SubscribeToEvents()
    {
        // Subscribe to service events
        SnackbarService.OnShow += HandleSnackbarShow;
        SnackbarService.OnRemove += HandleSnackbarRemove;

        // Subscribe to notification channels
        if (!string.IsNullOrEmpty(ChannelId))
        {
            var channelSubscription = NotificationSubscriber.Subscribe(
                $"{GlobalConstants.UserNotificationChannel}.{ChannelId}",
                HandleNotificationAsync);
            _subscriptions.Add(channelSubscription);
        }

        var globalSubscription = NotificationSubscriber.Subscribe(
            GlobalConstants.GlobalNotificationChannel,
            HandleNotificationAsync);
        _subscriptions.Add(globalSubscription);

        Logger.LogDebug("Subscribed to all events and channels");
    }

    private async Task HandleSnackbarShow(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _renderLock.WaitAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing snackbar {SnackbarId}", snackbar.Id);
        }
        finally
        {
            _renderLock.Release();
        }
    }

    private async Task HandleSnackbarRemove(string snackbarId)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _renderLock.WaitAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing snackbar {SnackbarId}", snackbarId);
        }
        finally
        {
            _renderLock.Release();
        }
    }

    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (_isDisposed || notification.Type != NotificationType.Toast)
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
                Duration = GetDurationForSeverity(notification.Severity),
                RequiresManualClose = GetRequiresManualClose(notification.Severity),
                CreatedAt = DateTime.UtcNow
            };

            await SnackbarService.Show(snackbar);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling notification as snackbar");
        }
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

    private static int GetDurationForSeverity(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => 5000,
            NotificationSeverity.Information => 5000,
            NotificationSeverity.Warning => 8000,
            NotificationSeverity.Error => 0,
            NotificationSeverity.Critical => 0,
            _ => 5000
        };
    }

    private static bool GetRequiresManualClose(NotificationSeverity severity)
    {
        return severity is NotificationSeverity.Error or NotificationSeverity.Critical;
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _isDisposed = true;

            try
            {
                // Unsubscribe from service events
                if (SnackbarService != null)
                {
                    SnackbarService.OnShow -= HandleSnackbarShow;
                    SnackbarService.OnRemove -= HandleSnackbarRemove;
                }

                // Dispose subscriptions
                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();

                _renderLock.Dispose();

                Logger.LogDebug("DropBearSnackbarContainer disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during DropBearSnackbarContainer disposal");
            }
        }

        await base.DisposeAsync(disposing);
    }
}
