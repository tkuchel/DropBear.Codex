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

/// <summary>
///     Container for displaying and managing page-level alerts.
///     Subscribes to alert services, notification channels, and global notifications.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    private readonly List<PageAlertInstance> _activeAlerts = new();
    private readonly List<PageAlertInstance> _alertsToInitialize = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IDisposable? _channelSubscription;

    [Parameter] public string? ChannelId { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        try
        {
            InitializeSubscriptions();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize PageAlertContainer");
            throw;
        }
    }

    private void InitializeSubscriptions()
    {
        if (PageAlertService is not null)
        {
            PageAlertService.OnAlert += HandleAlert;
            PageAlertService.OnClear += HandleClear;
            Logger.Debug("Subscribed to PageAlertService events");
        }
        else
        {
            Logger.Warning("PageAlertService is null during event subscription");
        }

        // Set up channel and global notifications
        var bag = DisposableBag.CreateBuilder();

        // Subscribe to channel-specific notifications if ChannelId provided
        if (!string.IsNullOrEmpty(ChannelId))
        {
            var channel = $"{GlobalConstants.UserNotificationChannel}.{ChannelId}";
            NotificationSubscriber.Subscribe(channel, HandleNotificationAsync).AddTo(bag);
            Logger.Debug("Subscribed to channel notifications for {ChannelId}", ChannelId);
        }

        // Always subscribe to global notifications
        NotificationSubscriber.Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync)
            .AddTo(bag);
        Logger.Debug("Subscribed to global notifications");

        _channelSubscription = bag.Build();
    }

    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.PageAlert || token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var alert = CreateAlertFromNotification(notification);
            await HandleAlert(alert);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification");
        }
    }

    private static PageAlertInstance CreateAlertFromNotification(Notification notification)
    {
        return new PageAlertInstance
        {
            Id = $"alert-{Guid.NewGuid():N}",
            Title = notification.Title ?? "Notification",
            Message = notification.Message,
            Type = MapNotificationSeverityToAlertType(notification.Severity),
            IsPermanent = ShouldBePermanent(notification.Severity),
            Duration = GetDurationForSeverity(notification.Severity)
        };
    }

    private async Task HandleAlert(PageAlertInstance alert)
    {
        try
        {
            await _semaphore.WaitAsync();
            _activeAlerts.Add(alert);
            _alertsToInitialize.Add(alert);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error queuing page alert: {Id}", alert.Id);
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
            await InitializeNewAlerts();
        }
    }

    private async Task InitializeNewAlerts()
    {
        foreach (var alert in _alertsToInitialize.ToList())
        {
            try
            {
                await EnsureJsModuleInitializedAsync("DropBearPageAlert");
                var result = await SafeJsInteropAsync<bool>(
                    "DropBearPageAlert.create",
                    alert.Id,
                    alert.Duration ?? 5000,
                    alert.IsPermanent
                );

                if (!result)
                {
                    Logger.Warning("Failed to create alert with ID: {AlertId}", alert.Id);
                    _activeAlerts.Remove(alert);
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing page alert: {Id}", alert.Id);
                _activeAlerts.Remove(alert);
                await InvokeAsync(StateHasChanged);
            }
            finally
            {
                _alertsToInitialize.Remove(alert);
            }
        }
    }

    private async void HandleClear()
    {
        try
        {
            await _semaphore.WaitAsync();
            var hidePromises = _activeAlerts.Select(alert => HideAlert(alert.Id)).ToList();
            await Task.WhenAll(hidePromises);
            _activeAlerts.Clear();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error clearing alerts");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> HideAlert(string alertId)
    {
        try
        {
            await EnsureJsModuleInitializedAsync("DropBearPageAlert");
            var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", alertId);
            if (!result)
            {
                Logger.Warning("Failed to hide alert with ID: {AlertId}", alertId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error hiding alert: {Id}", alertId);
            return false;
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await EnsureJsModuleInitializedAsync("DropBearPageAlert");
            await SafeJsInteropAsync<bool[]>("DropBearPageAlert.hideAll");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error hiding all alerts during cleanup");
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (PageAlertService is not null)
                {
                    PageAlertService.OnAlert -= HandleAlert;
                    PageAlertService.OnClear -= HandleClear;
                }

                _channelSubscription?.Dispose();
                _semaphore.Dispose();

                _activeAlerts.Clear();
                _alertsToInitialize.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during page alert container disposal");
            }
        }

        await base.DisposeAsync(disposing);
    }

    #region Helper Methods

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
            NotificationSeverity.Error => 0,
            NotificationSeverity.Critical => 0,
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

    #endregion
}
