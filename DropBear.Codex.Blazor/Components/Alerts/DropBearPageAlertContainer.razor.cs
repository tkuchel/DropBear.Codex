#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Serilog;
using AlertType = DropBear.Codex.Blazor.Enums.AlertType;
using DisposableBag = MessagePipe.DisposableBag;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying page alerts.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase, IDisposable
{
    private const int MaxChannelAlerts = 100;
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPageAlertContainer>();
    private readonly List<PageAlert> _channelAlerts = new();
    private readonly TimeSpan _debounceDuration = TimeSpan.FromMilliseconds(100);
    private IDisposable? _channelSubscription;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<PageAlert> CombinedAlerts => AlertService.Alerts.Concat(_channelAlerts);

    public void Dispose()
    {
        try
        {
            AlertService.OnChange -= HandleAlertChange;
            _channelSubscription?.Dispose();
            Logger.Debug("Disposed of alert subscriptions for {ChannelId}.", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing alert service for {ChannelId}.", ChannelId);
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (string.IsNullOrEmpty(ChannelId))
        {
            Logger.Error("ChannelId is null or empty during initialization.");
            return;
        }

        SubscribeToAlerts();
        Logger.Debug("Alert service initialized for channel: {ChannelId}", ChannelId);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        Logger.Debug("Channel Id set to {ChannelId} for PageAlertContainer.", ChannelId);
    }

    private void SubscribeToAlerts()
    {
        try
        {
            AlertService.OnChange += HandleAlertChange;
            _channelSubscription = SubscribeToChannelNotifications(ChannelId);
            Logger.Debug("Subscribed to channel notifications for {ChannelId}.", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to alerts for channel {ChannelId}.", ChannelId);
        }
    }

    private IDisposable SubscribeToChannelNotifications(string channelId)
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channelId, HandleNotification)
            .AddTo(bag);
        return bag.Build();
    }

    private ValueTask HandleNotification(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.PageAlert)
        {
            return ValueTask.CompletedTask;
        }

        var pageAlert = new PageAlert(notification.Title ?? "Alert", notification.Message,
            MapAlertType(notification.Severity));
        AddAlert(pageAlert);

        return ValueTask.CompletedTask;
    }

    private void AddAlert(PageAlert alert)
    {
        if (_channelAlerts.Count >= MaxChannelAlerts)
        {
            _channelAlerts.RemoveAt(0);
        }

        _channelAlerts.Add(alert);
        _ = DebouncedStateUpdate();
    }

    private void HandleAlertChange(object? sender, EventArgs e)
    {
        _ = DebouncedStateUpdate();
        Logger.Debug("Alert state change detected.");
    }

    public void ClearAlerts()
    {
        _channelAlerts.Clear();
        StateHasChanged();
    }

    public void ClearChannelAlerts()
    {
        _channelAlerts.Clear();
        _channelSubscription?.Dispose();
        _channelSubscription = SubscribeToChannelNotifications(ChannelId);
    }

    public void RemoveAlert(PageAlert alert)
    {
        _channelAlerts.Remove(alert);
        StateHasChanged();
    }

    private async Task DebouncedStateUpdate()
    {
        // await DebounceService.DebounceAsync(() => InvokeAsync(StateHasChanged), "PageAlertContainerUpdate",
        //     _debounceDuration);

        await InvokeAsync(StateHasChanged);
    }

    private AlertType MapAlertType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => AlertType.Information,
            NotificationSeverity.Success => AlertType.Success,
            NotificationSeverity.Warning => AlertType.Warning,
            NotificationSeverity.Error => AlertType.Danger,
            NotificationSeverity.Critical => AlertType.Danger,
            NotificationSeverity.NotSpecified => AlertType.Notification,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown NotificationSeverity value")
        };
    }
}
