#region

using System.Collections.Frozen;
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

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     Modern snackbar container optimized for .NET 9+ and Blazor Server.
///     Manages snackbar lifecycle, positioning, and notification subscriptions.
/// </summary>
public sealed partial class DropBearSnackbarContainer : DropBearComponentBase
{
    #region Constants & Mappings

    private static readonly FrozenDictionary<NotificationSeverity, SnackbarType> SeverityTypeMap =
        new Dictionary<NotificationSeverity, SnackbarType>
        {
            [NotificationSeverity.Success] = SnackbarType.Success,
            [NotificationSeverity.Information] = SnackbarType.Information,
            [NotificationSeverity.Warning] = SnackbarType.Warning,
            [NotificationSeverity.Error] = SnackbarType.Error,
            [NotificationSeverity.Critical] = SnackbarType.Error
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<NotificationSeverity, int> SeverityDurationMap =
        new Dictionary<NotificationSeverity, int>
        {
            [NotificationSeverity.Success] = 4000,
            [NotificationSeverity.Information] = 5000,
            [NotificationSeverity.Warning] = 7000,
            [NotificationSeverity.Error] = 0, // Manual close
            [NotificationSeverity.Critical] = 0 // Manual close
        }.ToFrozenDictionary();

    private static readonly FrozenSet<NotificationSeverity> ManualCloseSeverities =
        FrozenSet.ToFrozenSet([NotificationSeverity.Error, NotificationSeverity.Critical]);

    private static readonly FrozenDictionary<SnackbarPosition, string> PositionCssMap =
        new Dictionary<SnackbarPosition, string>
        {
            [SnackbarPosition.TopLeft] = "top-left",
            [SnackbarPosition.TopRight] = "top-right",
            [SnackbarPosition.BottomLeft] = "bottom-left",
            [SnackbarPosition.BottomRight] = "bottom-right"
        }.ToFrozenDictionary();

    #endregion

    #region Fields

    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly List<IDisposable> _subscriptions = new(2);
    private readonly CancellationTokenSource _containerCts = new();

    // Cached values
    private string _cachedPositionClass = PositionCssMap[SnackbarPosition.BottomRight];
    private string? _previousChannelId;
    private SnackbarPosition _previousPosition = SnackbarPosition.BottomRight;

    #endregion

    #region Injected Services

    [Inject] private ISnackbarService? SnackbarService { get; set; }
    [Inject] private IAsyncSubscriber<string, Notification> NotificationSubscriber { get; set; } = null!;

    #endregion

    #region Parameters

    /// <summary>
    ///     Optional channel ID for user-specific notifications.
    /// </summary>
    [Parameter]
    public string? ChannelId { get; set; }

    /// <summary>
    ///     Position of the snackbar container.
    /// </summary>
    [Parameter]
    public SnackbarPosition Position { get; set; } = SnackbarPosition.BottomRight;

    /// <summary>
    ///     Maximum number of snackbars to display simultaneously.
    /// </summary>
    [Parameter]
    public int MaxVisibleSnackbars { get; set; } = 5;

    /// <summary>
    ///     Whether to show newest snackbars first.
    /// </summary>
    [Parameter]
    public bool ShowNewestFirst { get; set; } = true;

    /// <summary>
    ///     Additional CSS classes for the container.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the CSS class for the current position with caching.
    /// </summary>
    private string PositionClass
    {
        get
        {
            if (_previousPosition != Position)
            {
                _previousPosition = Position;
                _cachedPositionClass = PositionCssMap.GetValueOrDefault(Position, PositionCssMap[SnackbarPosition.BottomRight]);
            }
            return _cachedPositionClass;
        }
    }

    /// <summary>
    ///     Gets the complete CSS class string.
    /// </summary>
    private string ContainerCssClasses =>
        $"dropbear-snackbar-container {PositionClass} {CssClass ?? ""}".Trim();

    /// <summary>
    ///     Gets the visible snackbars in the correct order.
    /// </summary>
    private IEnumerable<SnackbarInstance> VisibleSnackbars
    {
        get
        {
            var snackbars = SnackbarService?.GetActiveSnackbars();
            if (snackbars is null) return [];

            var orderedSnackbars = ShowNewestFirst
                ? snackbars.OrderByDescending(s => s.CreatedAt)
                : snackbars.OrderBy(s => s.CreatedAt);

            return orderedSnackbars.Take(MaxVisibleSnackbars);
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Initialize the container and set up subscriptions.
    /// </summary>
    protected override void OnInitialized()
    {
        try
        {
            SetupEventSubscriptions();
            base.OnInitialized();

            LogDebug("SnackbarContainer initialized with position: {Position}, channel: {Channel}",
                Position, ChannelId ?? "global");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize SnackbarContainer", ex);
            throw;
        }
    }

    /// <summary>
    ///     Update subscriptions when parameters change.
    /// </summary>
    protected override void OnParametersSet()
    {
        // Check if channel ID changed and requires subscription update
        if (ChannelId != _previousChannelId)
        {
            _previousChannelId = ChannelId;
            SetupEventSubscriptions();
        }

        base.OnParametersSet();
    }

    /// <summary>
    ///     Optimized render control.
    /// </summary>
    protected override bool ShouldRender()
    {
        // Only render if not disposed and we have potential snackbars to show
        return !IsDisposed && (SnackbarService?.GetActiveSnackbars()?.Any() ?? false);
    }

    /// <summary>
    ///     Clean up subscriptions and resources.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _containerCts.CancelAsync();

            await _operationSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
            try
            {
                CleanupSubscriptions();
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        finally
        {
            _containerCts.Dispose();
            _operationSemaphore.Dispose();
        }

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Event Management

    /// <summary>
    ///     Sets up event subscriptions for snackbar and notification events.
    /// </summary>
    private void SetupEventSubscriptions()
    {
        // Clean up existing subscriptions first
        CleanupSubscriptions();

        // Subscribe to snackbar service events
        if (SnackbarService is not null)
        {
            SnackbarService.OnShow += HandleSnackbarShowAsync;
            SnackbarService.OnRemove += HandleSnackbarRemoveAsync;
        }

        // Subscribe to notification channels
        if (!string.IsNullOrWhiteSpace(ChannelId))
        {
            var channelSubscription = NotificationSubscriber.Subscribe(
                $"{GlobalConstants.UserNotificationChannel}.{ChannelId}",
                HandleNotificationAsync);
            _subscriptions.Add(channelSubscription);
        }

        // Always subscribe to global notifications
        var globalSubscription = NotificationSubscriber.Subscribe(
            GlobalConstants.GlobalNotificationChannel,
            HandleNotificationAsync);
        _subscriptions.Add(globalSubscription);

        LogDebug("Event subscriptions established for {SubscriptionCount} channels", _subscriptions.Count);
    }

    /// <summary>
    ///     Cleans up all event subscriptions.
    /// </summary>
    private void CleanupSubscriptions()
    {
        // Unsubscribe from service events
        if (SnackbarService is not null)
        {
            SnackbarService.OnShow -= HandleSnackbarShowAsync;
            SnackbarService.OnRemove -= HandleSnackbarRemoveAsync;
        }

        // Dispose notification subscriptions
        foreach (var subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Expected during cleanup
            }
        }
        _subscriptions.Clear();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles snackbar show events with thread-safe state updates.
    /// </summary>
    private async Task HandleSnackbarShowAsync(SnackbarInstance snackbar)
    {
        if (IsDisposed) return;

        try
        {
            await _operationSemaphore.WaitAsync(_containerCts.Token);
            try
            {
                await QueueStateHasChangedAsync(() => Task.CompletedTask);
                LogDebug("Snackbar shown: {Id} - {Type}", snackbar.Id, snackbar.Type);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError("Error handling snackbar show: {Id}", ex, snackbar.Id);
        }
    }

    /// <summary>
    ///     Handles snackbar removal events.
    /// </summary>
    private async Task HandleSnackbarRemoveAsync(string snackbarId)
    {
        if (IsDisposed) return;

        try
        {
            await _operationSemaphore.WaitAsync(_containerCts.Token);
            try
            {
                await QueueStateHasChangedAsync(() => Task.CompletedTask);
                LogDebug("Snackbar removed: {Id}", snackbarId);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError("Error handling snackbar removal: {Id}", ex, snackbarId);
        }
    }

    /// <summary>
    ///     Handles incoming notifications and converts toast notifications to snackbars.
    /// </summary>
    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (IsDisposed || notification.Type != NotificationType.Toast || SnackbarService is null)
        {
            return;
        }

        try
        {
            var snackbar = CreateSnackbarFromNotification(notification);
            await SnackbarService.Show(snackbar, cancellationToken);

            LogDebug("Notification converted to snackbar: {Title} - {Severity}",
                notification.Title, notification.Severity);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError("Error handling notification: {Title}", ex, notification.Title);
        }
    }

    /// <summary>
    ///     Handles individual snackbar close events.
    /// </summary>
    private async Task HandleSnackbarCloseAsync(string snackbarId)
    {
        if (IsDisposed || SnackbarService is null) return;

        try
        {
            await SnackbarService.RemoveSnackbar(snackbarId);
        }
        catch (Exception ex)
        {
            LogError("Error closing snackbar: {Id}", ex, snackbarId);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates a snackbar instance from a notification with optimized mappings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SnackbarInstance CreateSnackbarFromNotification(Notification notification)
    {
        return new SnackbarInstance
        {
            Id = $"notification-{Guid.NewGuid():N}",
            Title = notification.Title ?? "Notification",
            Message = notification.Message,
            Type = SeverityTypeMap.GetValueOrDefault(notification.Severity, SnackbarType.Information),
            Duration = SeverityDurationMap.GetValueOrDefault(notification.Severity, 5000),
            RequiresManualClose = ManualCloseSeverities.Contains(notification.Severity),
            CreatedAt = DateTime.UtcNow,
            ShowDelay = 0
        };
    }

    #endregion
}
