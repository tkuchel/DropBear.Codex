#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     Container for displaying and managing page-level alerts using the new DropBearPageAlert JS module.
///     Subscribes to alert services, notification channels, and global notifications.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    // Active page alerts displayed
    private readonly List<PageAlertInstance> _activeAlerts = new();

    // Alerts that need to be initialized in JS after first render
    private readonly List<PageAlertInstance> _alertsToInitialize = new();

    // Lock to protect concurrent modifications
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Subscription to channel events
    private IDisposable? _channelSubscription;

    // JS module reference (cached once loaded in OnAfterRenderAsync)
    private IJSObjectReference? _jsModule;

    // JS module name
    private const string JsModuleName = JsModuleNames.PageAlerts;

    #region Parameters

    [Parameter] public string? ChannelId { get; set; }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        try
        {
            InitializeSubscriptions();
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize PageAlertContainer", ex);
            throw;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // Load/cache the JS module once (similar to "FileUploader style")
            _jsModule = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // If any alerts have queued up before the module was ready, initialize them
            if (_alertsToInitialize.Count > 0)
            {
                await InitializeNewAlerts().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError("Error during first render initialization for DropBearPageAlertContainer", ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Called by the base class to do final JS cleanup (e.g. hide all page alerts).
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        try
        {
            // Hide all active alerts via JS
            await _jsModule.InvokeAsync<bool[]>($"{JsModuleName}.hideAll").ConfigureAwait(false);
            LogDebug("All page alerts hidden during cleanup");
        }
        catch (JSDisconnectedException jsDiscEx)
        {
            LogWarning("JS disconnected while hiding all alerts: {Message}", jsDiscEx, jsDiscEx.Message);
        }
        catch (Exception ex)
        {
            LogError("Error hiding all alerts during cleanup", ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Dispose local resources (subscriptions, semaphores, lists).
    ///     The base class calls this after <see cref="CleanupJavaScriptResourcesAsync" />.
    /// </remarks>
    protected override async ValueTask DisposeCoreAsync()
    {
        try
        {
            // Unsubscribe from .NET events
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
        catch (ObjectDisposedException ode)
        {
            LogWarning("Error disposing page alert container: {Message}", ode, ode.Message);
        }
        catch (Exception ex)
        {
            LogError("Error disposing page alert container", ex);
        }
        finally
        {
            await base.DisposeCoreAsync().ConfigureAwait(false);
        }
    }

    #endregion

    #region Subscription / Notification Handling

    private void InitializeSubscriptions()
    {
        // Subscribe to local page alert service
        if (PageAlertService is not null)
        {
            PageAlertService.OnAlert += HandleAlert;
            PageAlertService.OnClear += HandleClear;
            LogDebug("Subscribed to PageAlertService events");
        }
        else
        {
            LogWarning("PageAlertService is null during event subscription");
        }

        // Subscribe to channel-based notifications
        var bag = DisposableBag.CreateBuilder();

        // Channel notifications
        if (!string.IsNullOrEmpty(ChannelId))
        {
            var channel = $"{GlobalConstants.UserNotificationChannel}.{ChannelId}";
            NotificationSubscriber.Subscribe(channel, HandleNotificationAsync).AddTo(bag);
            LogDebug("Subscribed to channel notifications for {ChannelId}", ChannelId);
        }

        // Global notifications
        NotificationSubscriber.Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync)
            .AddTo(bag);
        LogDebug("Subscribed to global notifications");

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
            await HandleAlert(alert).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Error handling notification", ex);
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

    #endregion

    #region Alert Handling

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
            LogError($"Error queuing page alert: {alert.Id}", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task InitializeNewAlerts()
    {
        // If we haven't loaded the module yet, wait for OnAfterRender to do it
        if (_jsModule is null)
        {
            return;
        }

        foreach (var alert in _alertsToInitialize.ToList())
        {
            try
            {
                // "DropBearPageAlert.create" -> (alert.Id, alert.Duration, alert.IsPermanent)
                var result = await _jsModule.InvokeAsync<bool>(
                    $"{JsModuleName}.create",
                    alert.Id,
                    alert.Duration ?? 5000,
                    alert.IsPermanent
                ).ConfigureAwait(false);

                if (!result)
                {
                    LogWarning("Failed to create alert with ID: {AlertId}", alert.Id);
                    _activeAlerts.Remove(alert);
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error showing page alert: {alert.Id}", ex);
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
            var hideTasks = _activeAlerts.Select(alert => HideAlert(alert.Id)).ToList();
            await Task.WhenAll(hideTasks);

            _activeAlerts.Clear();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogError("Error clearing alerts", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> HideAlert(string alertId)
    {
        if (_jsModule is null)
        {
            return false;
        }

        try
        {
            var result = await _jsModule.InvokeAsync<bool>(
                $"{JsModuleName}.hide",
                alertId
            ).ConfigureAwait(false);

            if (!result)
            {
                LogWarning("Failed to hide alert with ID: {AlertId}", alertId);
            }

            return result;
        }
        catch (Exception ex)
        {
            LogError($"Error hiding alert: {alertId}", ex);
            return false;
        }
    }

    #endregion

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
