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

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     Container for displaying and managing page-level alerts.
///     Subscribes to alert services, notification channels, and global notifications.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    private readonly List<PageAlertInstance> _activeAlerts = new();
    private readonly List<PageAlertInstance> _alertsToInitialize = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    ///     Stores the disposable subscription bag for channel/global notifications.
    /// </summary>
    private IDisposable? _channelSubscription;

    /// <summary>
    ///     The optional channel ID for subscribing to targeted user notifications.
    /// </summary>
    [Parameter]
    public string? ChannelId { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        try
        {
            SubscribeToPageAlertEvents();

            if (!string.IsNullOrEmpty(ChannelId))
            {
                SubscribeToChannelNotifications();
            }

            SubscribeToGlobalNotifications();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize PageAlertContainer");
            throw;
        }
    }

    /// <summary>
    ///     Subscribes to the <see cref="IPageAlertService" /> for receiving alert events.
    /// </summary>
    private void SubscribeToPageAlertEvents()
    {
        if (PageAlertService is null)
        {
            Logger.Warning("PageAlertService is null during event subscription");
            return;
        }

        PageAlertService.OnAlert += HandleAlert;
        PageAlertService.OnClear += HandleClear;

        Logger.Debug("Subscribed to PageAlertService events");
    }

    /// <summary>
    ///     Unsubscribes from the <see cref="IPageAlertService" /> to avoid memory leaks.
    /// </summary>
    private void UnsubscribeFromPageAlertEvents()
    {
        if (PageAlertService is not null)
        {
            PageAlertService.OnAlert -= HandleAlert;
            PageAlertService.OnClear -= HandleClear;

            Logger.Debug("Unsubscribed from PageAlertService events");
        }
    }

    /// <summary>
    ///     Subscribes to channel-specific notifications, if a <see cref="ChannelId" /> is provided.
    /// </summary>
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

    /// <summary>
    ///     Subscribes to the global notification channel for receiving global page alerts.
    /// </summary>
    private void SubscribeToGlobalNotifications()
    {
        var bag = DisposableBag.CreateBuilder();

        NotificationSubscriber.Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync)
            .AddTo(bag);

        _channelSubscription = bag.Build();

        Logger.Debug("Subscribed to global notifications");
    }

    /// <summary>
    ///     Handles incoming notifications, only processing those of type <see cref="NotificationType.PageAlert" />.
    /// </summary>
    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.PageAlert)
        {
            return;
        }

        try
        {
            var alert = new PageAlertInstance
            {
                Id = $"alert-{Guid.NewGuid():N}",
                Title = notification.Title ?? "Notification",
                Message = notification.Message,
                Type = MapNotificationSeverityToAlertType(notification.Severity),
                IsPermanent = ShouldBePermanent(notification.Severity),
                Duration = GetDurationForSeverity(notification.Severity)
            };

            await HandleAlert(alert);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification");
        }
    }

    /// <summary>
    ///     Determines whether an alert should remain indefinitely for a given <see cref="NotificationSeverity" />.
    /// </summary>
    private static bool ShouldBePermanent(NotificationSeverity severity)
    {
        return severity is NotificationSeverity.Critical or NotificationSeverity.Error;
    }

    /// <summary>
    ///     Gets a recommended duration (in ms) based on the <see cref="NotificationSeverity" />.
    ///     Zero indicates a permanent alert (no auto-dismiss).
    /// </summary>
    private static int GetDurationForSeverity(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => 5000,
            NotificationSeverity.Information => 5000,
            NotificationSeverity.Warning => 8000,
            NotificationSeverity.Error => 0, // Permanent
            NotificationSeverity.Critical => 0, // Permanent
            _ => 5000
        };
    }

    /// <summary>
    ///     Maps <see cref="NotificationSeverity" /> to a <see cref="PageAlertType" />.
    /// </summary>
    private static PageAlertType MapNotificationSeverityToAlertType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => PageAlertType.Success,
            NotificationSeverity.Information => PageAlertType.Info,
            NotificationSeverity.Warning => PageAlertType.Warning,
            NotificationSeverity.Error => PageAlertType.Error,
            NotificationSeverity.Critical => PageAlertType.Error,
            _ => PageAlertType.Info
        };
    }

    /// <summary>
    ///     Receives an alert instance from the alert service or notification, then displays it.
    /// </summary>
    private async Task HandleAlert(PageAlertInstance alert)
    {
        try
        {
            await ShowAlert(alert);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error queuing page alert: {Id}", alert.Id);
        }
    }

    /// <summary>
    ///     Adds a <see cref="PageAlertInstance" /> to the active list and schedules it for client-side initialization.
    /// </summary>
    private async Task ShowAlert(PageAlertInstance alert)
    {
        try
        {
            await _semaphore.WaitAsync();
            _activeAlerts.Add(alert);
            _alertsToInitialize.Add(alert);

            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // Attempt JS-side creation for any newly added alerts
        if (_alertsToInitialize.Count > 0)
        {
            foreach (var alert in _alertsToInitialize.ToList())
            {
                try
                {
                    var result = await SafeJsInteropAsync<bool>(
                        "DropBearPageAlert.create",
                        alert.Id,
                        alert.Duration ?? 5000,
                        alert.IsPermanent
                    );

                    if (!result)
                    {
                        Logger.Warning("Failed to create alert with ID: {AlertId}", alert.Id);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error showing page alert: {Id}", alert.Id);
                    // Remove any alert that fails to initialize
                    _activeAlerts.Remove(alert);
                    await InvokeAsync(StateHasChanged);
                }
                finally
                {
                    _alertsToInitialize.Remove(alert);
                }
            }
        }
    }

    /// <summary>
    ///     Handles a clear-all event from the alert service.
    /// </summary>
    private async void HandleClear()
    {
        try
        {
            var alertsToRemove = _activeAlerts.ToList();
            foreach (var alert in alertsToRemove)
            {
                await RemoveAlert(alert.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error clearing alerts");
        }
    }

    /// <summary>
    ///     Removes the alert with the given ID, first attempting a JS-side hide.
    /// </summary>
    private async Task RemoveAlert(string alertId)
    {
        try
        {
            await _semaphore.WaitAsync();

            var alert = _activeAlerts.FirstOrDefault(a => a.Id == alertId);
            if (alert is not null)
            {
                var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", alert.Id);
                if (!result)
                {
                    Logger.Warning("Failed to hide alert with ID: {AlertId}", alert.Id);
                }

                _activeAlerts.Remove(alert);
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                UnsubscribeFromPageAlertEvents();
                _channelSubscription?.Dispose();

                // Attempt to hide and remove all existing alerts
                foreach (var alert in _activeAlerts.ToList())
                {
                    try
                    {
                        var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", alert.Id);
                        if (!result)
                        {
                            Logger.Warning("Failed to hide alert with ID: {AlertId}", alert.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error disposing alert: {Id}", alert.Id);
                    }
                }

                _activeAlerts.Clear();
                _semaphore.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during page alert container disposal");
            }
        }

        await base.DisposeAsync(disposing);
    }
}
