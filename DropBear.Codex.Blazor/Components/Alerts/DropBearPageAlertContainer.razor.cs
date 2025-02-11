#region

using System.Collections.Concurrent;
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
    private const string JsModuleName = JsModuleNames.PageAlerts;
    private const int MaxQueueSize = 100;
    private const int BatchUpdateIntervalMs = 100;
    private readonly ConcurrentDictionary<string, PageAlertInstance> _activeAlerts = new();

    // Cancellation and synchronization
    private readonly CancellationTokenSource _batchingCts = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    // Thread-safe collections for alert management
    private readonly ConcurrentQueue<PageAlertInstance> _pendingAlerts = new();
    private Task? _batchUpdateTask;

    // Event subscriptions and JS module
    private IDisposable? _channelSubscription;
    private IJSObjectReference? _jsModule;

    /// <summary>
    ///     Gets or sets the channel ID for notification routing.
    /// </summary>
    [Parameter]
    public string? ChannelId { get; set; }

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        try
        {
            InitializeSubscriptions();
            StartBatchProcessor();
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
            await InitializeJsModuleAsync();
        }
        catch (Exception ex)
        {
            LogError("Error during first render initialization", ex);
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        try
        {
            // Attempt to hide all active alerts gracefully
            var hideAllResult = await _jsModule.InvokeAsync<bool[]>(
                $"{JsModuleName}API.hideAll",
                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
            );

            LogDebug("All page alerts hidden during cleanup. Success count: {SuccessCount}",
                hideAllResult.Count(success => success));
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("JS cleanup skipped: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Error hiding alerts during cleanup", ex);
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Cancel batch processing
            await _batchingCts.CancelAsync();
            if (_batchUpdateTask != null)
            {
                await _batchUpdateTask;
            }

            // Cleanup subscriptions and resources
            if (PageAlertService is not null)
            {
                PageAlertService.OnAlert -= HandleAlert;
                PageAlertService.OnClear -= HandleClear;
            }

            _channelSubscription?.Dispose();
            _initializationSemaphore.Dispose();
            _batchingCts.Dispose();

            // Clear collections
            _activeAlerts.Clear();
            while (_pendingAlerts.TryDequeue(out _)) { }
        }
        catch (Exception ex)
        {
            LogError("Error disposing page alert container", ex);
        }
        finally
        {
            await base.DisposeAsync();
        }
    }

    #endregion

    #region Alert Processing

    private async Task InitializeJsModuleAsync()
    {
        try
        {
            await _initializationSemaphore.WaitAsync();
            _jsModule = await GetJsModuleAsync(JsModuleName);

            // Process any queued alerts
            await ProcessPendingAlertsAsync();
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private void StartBatchProcessor()
    {
        _batchUpdateTask = Task.Run(async () =>
        {
            try
            {
                while (!_batchingCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(BatchUpdateIntervalMs, _batchingCts.Token);
                    await ProcessPendingAlertsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                LogError("Error in batch processor", ex);
            }
        }, _batchingCts.Token);
    }

    private async Task ProcessPendingAlertsAsync()
    {
        if (_jsModule is null || !_pendingAlerts.Any())
        {
            return;
        }

        // First, add pending alerts to the active collection so that they render.
        while (_pendingAlerts.TryDequeue(out var alert))
        {
            _activeAlerts.TryAdd(alert.Id, alert);
        }

        // Trigger a re-render so that the new alerts are placed in the DOM.
        await InvokeAsync(StateHasChanged);

        // Optionally, wait a tick to ensure the DOM updates
        await Task.Yield();

        // Now, for each alert in the active collection that hasn’t been initialized in JS,
        // invoke the JS interop to create the PageAlertManager.
        foreach (var alert in _activeAlerts.Values)
        {
            try
            {
                var result = await _jsModule.InvokeAsync<bool>(
                    $"{JsModuleName}API.create",
                    alert.Id,
                    alert.Duration ?? 5000,
                    alert.IsPermanent
                );

                if (!result)
                {
                    LogWarning("Failed to create alert: {AlertId}", alert.Id);
                }
            }
            catch (Exception ex)
            {
                LogError("Error processing alert {AlertId}", ex, alert.Id);
            }
        }
    }


    private async Task HandleAlert(PageAlertInstance alert)
    {
        try
        {
            if (_pendingAlerts.Count >= MaxQueueSize)
            {
                LogWarning("Alert queue full. Dropping alert: {AlertId}", alert.Id);
                return;
            }

            _pendingAlerts.Enqueue(alert);
            await ProcessPendingAlertsAsync();
        }
        catch (Exception ex)
        {
            LogError("Error handling alert {AlertId}", ex, alert.Id);
        }
    }

    private void HandleClear()
    {
        try
        {
            // Fire and forget the async clear operation
            _ = InvokeAsync(async () =>
            {
                try
                {
                    await _initializationSemaphore.WaitAsync();
                    var hideTasks = _activeAlerts.Keys
                        .Select(async alertId =>
                        {
                            if (_jsModule != null)
                            {
                                try
                                {
                                    await _jsModule.InvokeAsync<bool>($"{JsModuleName}API.hide", alertId);
                                }
                                catch (Exception ex)
                                {
                                    LogError($"Error hiding alert: {alertId}", ex);
                                }
                            }
                        });

                    await Task.WhenAll(hideTasks);
                    _activeAlerts.Clear();
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    LogError("Error in async clear operation", ex);
                }
                finally
                {
                    _initializationSemaphore.Release();
                }
            });
        }
        catch (Exception ex)
        {
            LogError("Error initiating clear operation", ex);
        }
    }

    #endregion

    #region Notification Handling

    private void InitializeSubscriptions()
    {
        // Subscribe to local page alert service
        if (PageAlertService is not null)
        {
            PageAlertService.OnAlert += HandleAlert;
            PageAlertService.OnClear += HandleClear;  // This needs to be void
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
            await InvokeAsync(() => HandleAlert(alert));
        }
        catch (Exception ex)
        {
            LogError("Error handling notification", ex);
        }
    }

    #endregion

    #region Helper Methods

    private static PageAlertInstance CreateAlertFromNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

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
