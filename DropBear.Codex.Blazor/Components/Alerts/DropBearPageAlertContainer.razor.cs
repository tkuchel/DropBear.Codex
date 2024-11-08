﻿#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
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
    private bool _isSubscribed;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<PageAlert> CombinedAlerts => AlertService.Alerts.Concat(_channelAlerts);

    public void Dispose()
    {
        DisposeResources();
    }

    private void DisposeResources()
    {
        if (_isSubscribed)
        {
            AlertService.OnAddAlert -= HandleAddAlert;
            AlertService.OnRemoveAlert -= HandleRemoveAlert;
            AlertService.OnClearAlerts -= HandleClearAlerts;
            _channelSubscription?.Dispose();
            Logger.Debug("Disposed of alert subscriptions for {ChannelId}.", ChannelId);
            _isSubscribed = false;
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
        if (!string.IsNullOrEmpty(ChannelId) && !_isSubscribed)
        {
            SubscribeToAlerts();
            Logger.Debug("Channel Id set to {ChannelId} for PageAlertContainer.", ChannelId);
        }
    }

    private void SubscribeToAlerts()
    {
        DisposeResources(); // Clean up any existing subscriptions
        try
        {
            AlertService.OnAddAlert += HandleAddAlert;
            AlertService.OnRemoveAlert += HandleRemoveAlert;
            AlertService.OnClearAlerts += HandleClearAlerts;

            _channelSubscription = SubscribeToChannelNotifications(ChannelId);
            _isSubscribed = true;
            Logger.Debug("Subscribed to channel notifications for {ChannelId}.", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to alerts for channel {ChannelId}.", ChannelId);
        }
    }

    private Task HandleClearAlerts(object sender, EventArgs e)
    {
        // _channelAlerts.Clear();
        _ = DebouncedStateUpdate();
        return Task.CompletedTask;
    }

    private Task HandleRemoveAlert(object sender, PageAlertEventArgs e)
    {
        // _channelAlerts.Remove(e.Alert);
        _ = DebouncedStateUpdate();
        return Task.CompletedTask;
    }

    private Task HandleAddAlert(object sender, PageAlertEventArgs e)
    {
        // AddAlert(e.Alert);
        _ = DebouncedStateUpdate();
        return Task.CompletedTask;
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
        Logger.Debug("Added alert of type {AlertType} with title '{AlertTitle}'.", alert.Type, alert.Title);
        _ = DebouncedStateUpdate();
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
        StateHasChanged();
    }

    public void RemoveAlert(PageAlert alert)
    {
        _channelAlerts.Remove(alert);
        StateHasChanged();
    }

    private async Task DebouncedStateUpdate()
    {
        await DebounceService.DebounceAsync(() => InvokeAsync(StateHasChanged), "PageAlertContainerUpdate",
            _debounceDuration);
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
