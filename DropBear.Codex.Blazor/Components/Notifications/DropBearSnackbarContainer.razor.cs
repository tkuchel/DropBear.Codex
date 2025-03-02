#region

using System.Runtime.CompilerServices;
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

/// <summary>
///     A container component for displaying snackbars. It subscribes to snackbar service events
///     and notification channels, and triggers UI updates accordingly.
/// </summary>
public sealed partial class DropBearSnackbarContainer : DropBearComponentBase
{
    #region Event Subscriptions

    /// <summary>
    ///     Subscribes to snackbar service events and notification channels.
    /// </summary>
    private void SubscribeToEvents()
    {
        // Unsubscribe from any existing subscriptions
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();

        // Subscribe to snackbar events.
        if (SnackbarService != null)
        {
            SnackbarService.OnShow += HandleSnackbarShow;
            SnackbarService.OnRemove += HandleSnackbarRemove;
        }

        // Subscribe to notification channels.
        if (!string.IsNullOrEmpty(_channelId))
        {
            var channelSubscription = NotificationSubscriber.Subscribe(
                $"{GlobalConstants.UserNotificationChannel}.{_channelId}",
                HandleNotificationAsync);
            _subscriptions.Add(channelSubscription);
        }

        var globalSubscription = NotificationSubscriber.Subscribe(
            GlobalConstants.GlobalNotificationChannel,
            HandleNotificationAsync);
        _subscriptions.Add(globalSubscription);

        Logger.LogDebug("Subscribed to all events and channels");
    }

    #endregion

    #region Disposal

    /// <summary>
    ///     Disposes of the component and unsubscribes from events.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            // Unsubscribe from service events.
            if (SnackbarService is not null)
            {
                SnackbarService.OnShow -= HandleSnackbarShow;
                SnackbarService.OnRemove -= HandleSnackbarRemove;
            }

            // Dispose all subscriptions.
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

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Fields & Constants

    private readonly SemaphoreSlim _renderLock = new(1, 1);
    private readonly List<IDisposable> _subscriptions = new();
    private bool _isDisposed;

    // Backing fields for parameters
    private string? _channelId;
    private SnackbarPosition _position = SnackbarPosition.BottomRight;

    // Cached position class

    // Flag to track if component should render
    private bool _shouldRender = true;

    [Inject] private ISnackbarService? SnackbarService { get; set; }
    [Inject] private IAsyncSubscriber<string, Notification> NotificationSubscriber { get; set; } = null!;
    [Inject] private new ILogger<DropBearSnackbarContainer> Logger { get; set; } = null!;

    #endregion

    #region Parameters

    /// <summary>
    ///     Optional channel ID used to subscribe to user-specific notifications.
    /// </summary>
    [Parameter]
    public string? ChannelId
    {
        get => _channelId;
        set
        {
            if (_channelId != value)
            {
                _channelId = value;
                // Don't set _shouldRender here because we need to resubscribe to events
            }
        }
    }

    /// <summary>
    ///     Specifies the position of the snackbar container.
    /// </summary>
    [Parameter]
    public SnackbarPosition Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                UpdatePositionClass();
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets the CSS class corresponding to the chosen position.
    /// </summary>
    private string PositionClass { get; set; } = "bottom-right";

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Initializes the component by subscribing to events.
    /// </summary>
    protected override void OnInitialized()
    {
        try
        {
            UpdatePositionClass();
            SubscribeToEvents();
            base.OnInitialized();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize DropBearSnackbarContainer");
            throw;
        }
    }

    /// <summary>
    ///     Updates subscriptions when parameters change.
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // If the channel ID changed, we need to resubscribe
        if (_channelId != null && _subscriptions.Count == 0)
        {
            SubscribeToEvents();
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles the snackbar show event by triggering a UI update.
    /// </summary>
    /// <param name="snackbar">The snackbar instance to show.</param>
    private async Task HandleSnackbarShow(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _renderLock.WaitAsync();

            // Mark for rendering and invoke state change
            _shouldRender = true;
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

    /// <summary>
    ///     Handles the snackbar remove event by triggering a UI update.
    /// </summary>
    /// <param name="snackbarId">The ID of the snackbar being removed.</param>
    private async Task HandleSnackbarRemove(string snackbarId)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _renderLock.WaitAsync();

            // Mark for rendering and invoke state change
            _shouldRender = true;
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

    /// <summary>
    ///     Handles incoming notifications and, if of type Toast, creates and shows a snackbar.
    /// </summary>
    /// <param name="notification">The received notification.</param>
    /// <param name="token">A cancellation token.</param>
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

            await SnackbarService?.Show(snackbar, token)!;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling notification as snackbar");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Updates the position class based on the Position property.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePositionClass()
    {
        PositionClass = _position switch
        {
            SnackbarPosition.TopLeft => "top-left",
            SnackbarPosition.TopRight => "top-right",
            SnackbarPosition.BottomLeft => "bottom-left",
            _ => "bottom-right"
        };
    }

    /// <summary>
    ///     Maps notification severity to snackbar type.
    /// </summary>
    /// <param name="severity">The notification severity.</param>
    /// <returns>The corresponding snackbar type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    ///     Gets the appropriate duration for a notification based on its severity.
    /// </summary>
    /// <param name="severity">The notification severity.</param>
    /// <returns>The duration in milliseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    ///     Determines if a notification requires manual closing based on its severity.
    /// </summary>
    /// <param name="severity">The notification severity.</param>
    /// <returns>True if manual closing is required, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetRequiresManualClose(NotificationSeverity severity)
    {
        return severity is NotificationSeverity.Error or NotificationSeverity.Critical;
    }

    #endregion
}
