#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Events;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    private const int MaxChannelAlerts = 100;
    private const int StateUpdateDebounceMs = 100;
    private readonly string _containerId;

    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private IDisposable? _channelSubscription;
    private bool _isSubscribed;

    public DropBearPageAlertContainer()
    {
        _containerId = $"alert-container-{ComponentId}";
    }

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<PageAlert> CombinedAlerts
    {
        get
        {
            if (string.IsNullOrEmpty(ChannelId) || !AlertChannelManager.IsValidChannel(ChannelId))
            {
                return [];
            }

            // Filter alerts by channel if needed
            return AlertService.Alerts
                .Where(a => string.IsNullOrEmpty(a.ChannelId) || a.ChannelId == ChannelId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(MaxChannelAlerts);
        }
    }


    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitializeContainerAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (!string.IsNullOrEmpty(ChannelId) &&
            AlertChannelManager.IsValidChannel(ChannelId) &&
            !_isSubscribed)
        {
            await InitializeContainerAsync();
        }
    }

    private async Task InitializeContainerAsync()
    {
        if (string.IsNullOrEmpty(ChannelId))
        {
            Logger.Error("ChannelId is null or empty during initialization");
            return;
        }

        try
        {
            await SubscribeToAlertsAsync();
            Logger.Debug("Alert container initialized for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize alert container for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private async Task SubscribeToAlertsAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        UnsubscribeFromAlerts();

        try
        {
            AlertService.OnAddAlert += HandleAddAlert;
            AlertService.OnRemoveAlert += HandleRemoveAlert;
            AlertService.OnClearAlerts += HandleClearAlerts;
            await SubscribeToChannelNotificationsAsync(ChannelId);
            _isSubscribed = true;

            Logger.Debug("Subscribed to alerts for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to alerts for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private async Task SubscribeToChannelNotificationsAsync(string channelId)
    {
        var bag = DisposableBag.CreateBuilder();

        // Convert the async void to async Task and handle exceptions
        await InvokeAsync(() =>
        {
            try
            {
                NotificationSubscriber.Subscribe<string, Notification>(
                    channelId,
                    async (notification, token) =>
                    {
                        try
                        {
                            await HandleNotification(notification, token);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error handling notification in channel: {ChannelId}", channelId);
                        }
                    }
                ).AddTo(bag);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error subscribing to channel notifications: {ChannelId}", channelId);
                throw;
            }
        });

        _channelSubscription = bag.Build();
    }

    private void UnsubscribeFromAlerts()
    {
        if (!_isSubscribed)
        {
            return;
        }

        AlertService.OnAddAlert -= HandleAddAlert;
        AlertService.OnRemoveAlert -= HandleRemoveAlert;
        AlertService.OnClearAlerts -= HandleClearAlerts;

        _channelSubscription?.Dispose();
        _channelSubscription = null;

        Logger.Debug("Unsubscribed from alerts for channel: {ChannelId}", ChannelId);
        _isSubscribed = false;
    }

    private async Task HandleAddAlert(object sender, PageAlertEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        await AddAlertAsync(e.Alert);
    }

    private async Task HandleRemoveAlert(object sender, PageAlertEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        await RemoveAlertAsync(e.Alert);
    }

    private async Task HandleClearAlerts(object sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        await ClearAlertsAsync();
    }

    private async ValueTask HandleNotification(Notification notification, CancellationToken token)
    {
        if (IsDisposed || notification.Type != NotificationType.PageAlert)
        {
            return;
        }

        var pageAlert = new PageAlert(
            notification.Title ?? "Alert",
            notification.Message,
            MapAlertType(notification.Severity)
        );

        await AddAlertAsync(pageAlert);
    }

    private async Task AddAlertAsync(PageAlert alert)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            await AlertService.AddAlertAsync(
                alert.Title,
                alert.Message,
                alert.Type,
                alert.IsDismissible,
                ChannelId
            );

            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task RemoveAlertAsync(PageAlert alert)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            await AlertService.RemoveAlertAsync(alert.Id);
            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task ClearAlertsAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            await AlertService.ClearAlertsAsync();
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

        var debounceService = DebounceService;

        try
        {
            await debounceService.DebounceAsync(
                async () =>
                {
                    try
                    {
                        if (!IsDisposed)
                        {
                            await InvokeAsync(StateHasChanged);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error during state update");
                    }
                },
                "PageAlertContainerUpdate",
                TimeSpan.FromMilliseconds(StateUpdateDebounceMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in debounced state update");
        }
    }

    private static AlertType MapAlertType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => AlertType.Information,
            NotificationSeverity.Success => AlertType.Success,
            NotificationSeverity.Warning => AlertType.Warning,
            NotificationSeverity.Error or NotificationSeverity.Critical => AlertType.Danger,
            NotificationSeverity.NotSpecified => AlertType.Notification,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
    }

    private async Task OnCloseAlert(PageAlert alert)
    {
        if (IsDisposed)
        {
            return;
        }

        await RemoveAlertAsync(alert);
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await SafeJsVoidInteropAsync("alertContainer.cleanup", _containerId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during alert container cleanup: {ContainerId}", _containerId);
        }
        finally
        {
            UnsubscribeFromAlerts();
            _updateLock.Dispose();
        }
    }
}
