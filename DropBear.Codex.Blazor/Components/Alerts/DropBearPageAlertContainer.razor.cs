#region

using System.Collections.Frozen;
using System.Threading.Channels;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
/// Modern alert container optimized for .NET 8+ with improved performance and accessibility.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase
{
    private const int MaxVisibleAlerts = 5;
    private const int DefaultDuration = 5000;

    // Use FrozenSet for better performance in .NET 8+
    private static readonly FrozenSet<string> CriticalSeverities = new[]
    {
        nameof(NotificationSeverity.Critical), nameof(NotificationSeverity.Error)
    }.ToFrozenSet();

    // Use modern collection types
    private readonly Dictionary<string, AlertInstance> _activeAlerts = new(StringComparer.Ordinal);
    private readonly Channel<PageAlertInstance> _alertChannel;
    private readonly ChannelWriter<PageAlertInstance> _alertWriter;
    private readonly ChannelReader<PageAlertInstance> _alertReader;

    private ErrorBoundary? _errorBoundary;
    private IDisposable? _notificationSubscription;
    private Task? _processingTask;

    [Parameter] public string ChannelId { get; set; } = "Alerts";

    public DropBearPageAlertContainer()
    {
        // Use channels for better async processing in .NET 8+
        var options = new BoundedChannelOptions(MaxVisibleAlerts * 2)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        };

        _alertChannel = Channel.CreateBounded<PageAlertInstance>(options);
        _alertWriter = _alertChannel.Writer;
        _alertReader = _alertChannel.Reader;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Subscribe to PageAlertService events
        PageAlertService.OnAlert += HandleAlertAsync;
        PageAlertService.OnClear += HandleClear;

        // Start background processing
        _processingTask = ProcessAlertsAsync(ComponentToken);

        // Subscribe to notifications
        await SubscribeToNotificationsAsync();
    }

    protected override ValueTask InitializeComponentAsync()
    {
        LogDebug("Alert container initialized");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Background task for processing alerts using modern async patterns.
    /// </summary>
    private async Task ProcessAlertsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var alert in _alertReader.ReadAllAsync(cancellationToken))
            {
                await ProcessSingleAlertAsync(alert);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            LogError("Error in alert processing background task", ex);
        }
    }

    /// <summary>
    /// Process a single alert with optimized state management.
    /// </summary>
    private async Task ProcessSingleAlertAsync(PageAlertInstance alertData)
    {
        try
        {
            // Remove oldest non-permanent alert if at capacity
            if (_activeAlerts.Count >= MaxVisibleAlerts)
            {
                var oldestAlert = _activeAlerts.Values
                    .Where(a => !a.IsPermanent)
                    .MinBy(a => a.CreatedAt);

                if (oldestAlert != null)
                {
                    await RemoveAlertAsync(oldestAlert.Id);
                }
            }

            var alert = new AlertInstance
            {
                Id = alertData.Id,
                Title = alertData.Title,
                Message = alertData.Message,
                Type = alertData.Type,
                Duration = alertData.Duration ?? DefaultDuration,
                IsPermanent = alertData.IsPermanent,
                CreatedAt = alertData.CreatedAt
            };

            _activeAlerts[alert.Id] = alert;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogError("Error processing alert: {AlertId}", ex, alertData.Id);
        }
    }

    /// <summary>
    /// Subscribe to notifications using improved async patterns.
    /// </summary>
    private Task SubscribeToNotificationsAsync()
    {
        try
        {
            _notificationSubscription?.Dispose();

            _notificationSubscription = NotificationSubscriber.Subscribe(
                ChannelId,
                new AsyncMessageHandler<Notification>(HandleNotificationAsync)
            );

            LogDebug("Successfully subscribed to notifications channel: {ChannelId}", ChannelId);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError("Failed to subscribe to notifications", ex);
            throw;
        }
    }

    /// <summary>
    /// Handle alerts from PageAlertService.
    /// </summary>
    private async Task HandleAlertAsync(PageAlertInstance alertData)
    {
        if (IsDisposed) return;

        // Use channel for thread-safe queuing
        if (!await _alertWriter.WaitToWriteAsync(ComponentToken))
        {
            LogWarning("Alert channel is closed, cannot add alert: {AlertId}", alertData.Id);
            return;
        }

        if (!_alertWriter.TryWrite(alertData))
        {
            LogWarning("Failed to queue alert: {AlertId}", alertData.Id);
        }
    }

    /// <summary>
    /// Handle notifications with improved mapping.
    /// </summary>
    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (IsDisposed || notification?.Message == null) return;

        try
        {
            var (alertType, isPermanent, duration) = MapNotificationToAlert(notification.Severity);

            var alert = new PageAlertInstance
            {
                Id = $"notification-{Guid.NewGuid():N}",
                Title = notification.Title ?? "Notification",
                Message = notification.Message,
                Type = alertType,
                IsPermanent = isPermanent,
                Duration = duration,
                CreatedAt = DateTime.UtcNow
            };

            await HandleAlertAsync(alert);
        }
        catch (Exception ex)
        {
            LogError("Error handling notification", ex);
        }
    }

    /// <summary>
    /// Optimized mapping using pattern matching.
    /// </summary>
    private static (PageAlertType Type, bool IsPermanent, int Duration) MapNotificationToAlert(
        NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Critical => (PageAlertType.Error, true, 0),
            NotificationSeverity.Error => (PageAlertType.Error, false, 8000),
            NotificationSeverity.Warning => (PageAlertType.Warning, false, 7000),
            NotificationSeverity.Success => (PageAlertType.Success, false, 5000),
            NotificationSeverity.Information => (PageAlertType.Info, false, 6000),
            _ => (PageAlertType.Info, false, 6000)
        };
    }

    /// <summary>
    /// Clear all alerts.
    /// </summary>
    private void HandleClear()
    {
        if (IsDisposed) return;

        _activeAlerts.Clear();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Remove specific alert.
    /// </summary>
    private async Task RemoveAlertAsync(string alertId)
    {
        if (IsDisposed || !_activeAlerts.Remove(alertId)) return;

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Get prioritized alerts for rendering.
    /// </summary>
    private IEnumerable<AlertInstance> GetPrioritizedAlerts()
    {
        return _activeAlerts.Values
            .OrderByDescending(a => a.IsPermanent)
            .ThenByDescending(a => GetAlertPriority(a.Type))
            .ThenByDescending(a => a.CreatedAt)
            .Take(MaxVisibleAlerts);
    }

    /// <summary>
    /// Get priority value for alert types.
    /// </summary>
    private static int GetAlertPriority(PageAlertType type) => type switch
    {
        PageAlertType.Error => 4,
        PageAlertType.Warning => 3,
        PageAlertType.Success => 2,
        PageAlertType.Info => 1,
        _ => 0
    };

    /// <summary>
    /// Recover error boundary on parameter changes.
    /// </summary>
    protected override void OnParametersSet()
    {
        _errorBoundary?.Recover();
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        // Complete the channel
        _alertWriter.Complete();

        // Wait for processing to complete
        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        // Unsubscribe from events
        if (PageAlertService != null)
        {
            PageAlertService.OnAlert -= HandleAlertAsync;
            PageAlertService.OnClear -= HandleClear;
        }

        // Dispose notification subscription
        _notificationSubscription?.Dispose();

        // Clear alerts
        _activeAlerts.Clear();

        await base.DisposeAsyncCore();
    }

    /// <summary>
    /// Enhanced alert instance with metadata.
    /// </summary>
    private sealed class AlertInstance : PageAlertInstance
    {
        public DropBearPageAlert? Reference { get; set; }
        public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Async message handler implementation.
    /// </summary>
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
}
