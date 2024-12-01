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

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    private readonly List<PageAlertInstance> _activeAlerts = new();
    private readonly List<PageAlertInstance> _alertsToInitialize = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IDisposable? _channelSubscription;

    [Parameter] public string? ChannelId { get; set; }

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

    private void UnsubscribeFromPageAlertEvents()
    {
        if (PageAlertService is not null)
        {
            PageAlertService.OnAlert -= HandleAlert;
            PageAlertService.OnClear -= HandleClear;
            Logger.Debug("Unsubscribed from PageAlertService events");
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

    private static bool ShouldBePermanent(NotificationSeverity severity)
    {
        return severity is NotificationSeverity.Critical or NotificationSeverity.Error;
    }

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

    private async Task ShowAlert(PageAlertInstance alert)
    {
        try
        {
            await _semaphore.WaitAsync();

            _activeAlerts.Add(alert);
            await InvokeAsync(StateHasChanged);

            // Add alert to the initialization queue
            _alertsToInitialize.Add(alert);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (_alertsToInitialize.Count > 0)
        {
            foreach (var alert in _alertsToInitialize.ToList())
            {
                try
                {
                    if (alert.Duration != null)
                    {
                        var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", $"alert-{alert.Id}");
                        if (!result)
                        {
                            Logger.Warning("Failed to hide alert with ID: {AlertId}", alert.Id);
                        }
                    }
                    else
                    {
                        var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.create", $"alert-{alert.Id}",
                            alert.Duration, alert.IsPermanent);
                        if (!result)
                        {
                            Logger.Warning("Failed to create alert with ID: {AlertId}", alert.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error showing page alert: {Id}", alert.Id);
                    // Clean up on failure
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

    private async Task RemoveAlert(string alertId)
    {
        try
        {
            await _semaphore.WaitAsync();

            var alert = _activeAlerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                try
                {
                    var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", $"alert-{alertId}");
                    if (!result)
                    {
                        Logger.Warning("Failed to hide alert with ID: {AlertId}", alert.Id);
                    }

                    _activeAlerts.Remove(alert);
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error removing alert: {Id}", alertId);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                UnsubscribeFromPageAlertEvents();
                _channelSubscription?.Dispose();

                foreach (var alert in _activeAlerts.ToList())
                {
                    try
                    {
                        var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", $"alert-{alert.Id}");
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
