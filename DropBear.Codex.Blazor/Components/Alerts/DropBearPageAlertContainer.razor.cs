#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     Container for displaying and managing page-level alerts using the DropBearPageAlert JS module.
///     Handles alert queuing, batching, and lifecycle management with optimizations for Blazor Server.
/// </summary>
/// <remarks>
///     This component manages alert notifications through multiple channels:
///     - Direct page alerts via PageAlertService
///     - Channel-specific notifications
///     - Global notifications
///     Performance optimizations include:
///     - Batched UI updates
///     - Concurrent queue for alert management
///     - Automatic cleanup of stale alerts
///     - Memory-efficient event handling
/// </remarks>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    // Maximum alerts to show at once to limit DOM size and memory usage
    private const int MaxVisibleAlerts = 5;
    private const int DefaultDuration = 5000;

    // Thread-safe collection to store active alerts
    private readonly ConcurrentDictionary<string, AlertInstance> _activeAlerts = new();
    private IJSObjectReference? _alertModule;

    private ErrorBoundary? _errorBoundary;
    private bool _isModuleInitialized;
    private IDisposable? _notificationSubscription;

    [Parameter] public string ChannelId { get; set; } = "Alerts";

    /// <summary>
    ///     Initialize and set up event subscriptions
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Subscribe to PageAlertService events
        PageAlertService.OnAlert += HandleAlertAsync;
        PageAlertService.OnClear += HandleClear;

        // Subscribe to notification system
        try
        {
            await SubscribeToNotificationsAsync();
        }
        catch (Exception ex)
        {
            LogError("Failed to subscribe to notifications", ex);
        }
    }

    /// <summary>
    ///     Initialize JS functionality when component is first rendered
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        try
        {
            // Load the module first
            var alertModuleResult = await GetJsModuleAsync("DropBearPageAlert");

            if (!alertModuleResult.IsSuccess)
            {
                LogError("Failed to load alert container JSmodule", alertModuleResult.Exception);
                return;
            }

            _alertModule = alertModuleResult.Value;

            // Give the module a moment to register in the global scope
            await Task.Delay(50);

            // Now check if it's initialized, but use the module reference directly
            _isModuleInitialized = await _alertModule.InvokeAsync<bool>("DropBearPageAlertAPI.isInitialized");

            if (!_isModuleInitialized)
            {
                // Initialize through the module reference, not global scope
                await _alertModule.InvokeVoidAsync("initialize");
                _isModuleInitialized = true;
            }

            LogDebug("Alert container initialized with JS module");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize alert container JS module", ex);
        }
    }

    /// <summary>
    ///     Subscribes to the notification system
    /// </summary>
    private Task SubscribeToNotificationsAsync()
    {
        try
        {
            // Make sure any existing subscription is disposed
            _notificationSubscription?.Dispose();

            // Create an async message handler for the notification
            var handler = new AsyncMessageHandler<Notification>(async (notification, cancellationToken) =>
            {
                await HandleNotificationAsync(notification);
            });

            // Subscribe to the "Alerts" channel using the MessagePipe interface
            _notificationSubscription = NotificationSubscriber.Subscribe(
                ChannelId, // The channel key
                handler // The message handler
            );

            LogDebug("Successfully subscribed to notifications channel");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError("Failed to subscribe to notifications", ex);
            throw;
        }
    }

    /// <summary>
    ///     Recover the error boundary when parameters change
    /// </summary>
    protected override void OnParametersSet()
    {
        _errorBoundary?.Recover();
    }

    /// <summary>
    ///     Handles incoming alert requests from the PageAlertService
    /// </summary>
    private async Task HandleAlertAsync(PageAlertInstance alertData)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // Ensure we don't exceed maximum visible alerts by removing oldest non-permanent alert if needed
            if (_activeAlerts.Count >= MaxVisibleAlerts)
            {
                var oldestAlert = _activeAlerts.Values
                    .Where(a => !a.IsPermanent)
                    .OrderBy(a => a.Id)
                    .FirstOrDefault();

                if (oldestAlert != null)
                {
                    await RemoveAlertAsync(oldestAlert.Id);
                }
            }

            // Create extended alert instance
            var alert = new AlertInstance
            {
                Id = alertData.Id,
                Title = alertData.Title,
                Message = alertData.Message,
                Type = alertData.Type,
                Duration = alertData.Duration ?? DefaultDuration,
                IsPermanent = alertData.IsPermanent
            };

            // Add to collection and queue state update
            if (_activeAlerts.TryAdd(alert.Id, alert))
            {
                await QueueStateHasChangedAsync(() => { });
            }
        }
        catch (Exception ex)
        {
            LogError("Error handling alert", ex);
        }
    }

    /// <summary>
    ///     Handles notifications from the notification system
    /// </summary>
    private async Task HandleNotificationAsync(Notification notification)
    {
        if (IsDisposed || notification?.Message == null)
        {
            return;
        }

        try
        {
            // Map notification severity to alert type
            var alertType = notification.Severity switch
            {
                NotificationSeverity.Critical => PageAlertType.Error,
                NotificationSeverity.Error => PageAlertType.Error,
                NotificationSeverity.Warning => PageAlertType.Warning,
                NotificationSeverity.Success => PageAlertType.Success,
                NotificationSeverity.Information => PageAlertType.Info,
                _ => PageAlertType.Info
            };

            // Determine if the alert should be permanent based on severity
            var isPermanent = notification.Severity is NotificationSeverity.Critical or NotificationSeverity.Error;

            // Set duration based on alert type if not permanent
            var duration = !isPermanent
                ? alertType switch
                {
                    PageAlertType.Success => 5000,
                    PageAlertType.Error => 8000,
                    PageAlertType.Warning => 7000,
                    _ => 6000
                }
                : 3500;

            var alert = new PageAlertInstance
            {
                Id = $"notification-{Guid.NewGuid():N}",
                Title = notification.Title ?? "Notification",
                Message = notification.Message,
                Type = alertType,
                IsPermanent = isPermanent,
                Duration = duration
            };

            await HandleAlertAsync(alert);
        }
        catch (Exception ex)
        {
            LogError("Error handling notification", ex);
        }
    }

    /// <summary>
    ///     Handles clearing all alerts
    /// </summary>
    private async void HandleClear()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (_isModuleInitialized && _alertModule != null)
            {
                await SafeJsVoidInteropAsync("DropBearPageAlertAPI.hideAll");
            }

            _activeAlerts.Clear();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogError("Error clearing alerts", ex);
        }
    }

    /// <summary>
    ///     Removes a specific alert by ID
    /// </summary>
    private async Task RemoveAlertAsync(string alertId)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (_activeAlerts.TryRemove(alertId, out var alert) && alert.Reference != null)
            {
                // Alert will be removed from DOM after animation completes
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            LogError("Error removing alert", ex);
        }
    }

    /// <summary>
    ///     Returns alerts in priority order for rendering
    /// </summary>
    private IEnumerable<AlertInstance> GetPrioritizedAlerts()
    {
        return _activeAlerts.Values
            .OrderByDescending(a => a.IsPermanent)
            .ThenByDescending(a => GetAlertPriority(a.Type))
            .ThenByDescending(a => a.Id) // Newest first (assuming ID has temporal ordering)
            .Take(MaxVisibleAlerts);
    }

    /// <summary>
    ///     Gets a numeric priority value for alert types to enable sorting
    /// </summary>
    private static int GetAlertPriority(PageAlertType type)
    {
        return type switch
        {
            PageAlertType.Error => 4,
            PageAlertType.Warning => 3,
            PageAlertType.Success => 2,
            PageAlertType.Info => 1,
            _ => 0
        };
    }

    /// <summary>
    ///     Clean up JS resources
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_alertModule != null && _isModuleInitialized)
        {
            try
            {
                await SafeJsVoidInteropAsync("DropBearPageAlertAPI.hideAll");
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                LogDebug("Failed to clean up JS resources", objectDisposedException);
            }
            catch (Exception ex)
            {
                LogError("Failed to clean up JS resources", ex);
            }
        }
    }

    /// <summary>
    ///     Ensures all resources are properly disposed
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        // Unsubscribe from PageAlertService events
        if (PageAlertService != null)
        {
            PageAlertService.OnAlert -= HandleAlertAsync;
            PageAlertService.OnClear -= HandleClear;
        }

        // Dispose notification subscription
        _notificationSubscription?.Dispose();

        // Clear alerts dictionary
        _activeAlerts.Clear();

        await base.DisposeAsyncCore();
    }

    // Helper class to implement IAsyncMessageHandler<T>
    private sealed class AsyncMessageHandler<T> : IAsyncMessageHandler<T>
    {
        private readonly Func<T, CancellationToken, ValueTask> _handler;

        public AsyncMessageHandler(Func<T, CancellationToken, ValueTask> handler)
        {
            _handler = handler;
        }

        public ValueTask HandleAsync(T message, CancellationToken cancellationToken)
        {
            return _handler(message, cancellationToken);
        }
    }

    // Extended alert model to include component reference
    private sealed class AlertInstance : PageAlertInstance
    {
        public DropBearPageAlert? Reference { get; set; }
    }
}
