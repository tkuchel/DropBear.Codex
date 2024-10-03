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

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying page alerts.
/// </summary>
public sealed partial class DropBearPageAlertContainer : DropBearComponentBase, IDisposable
{
    private const int MaxChannelAlerts = 100; // Or whatever number is appropriate
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPageAlertContainer>();
    private readonly List<PageAlert> _channelAlerts = [];
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(100); // Adjust as needed
    private IDisposable? _disposable;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<PageAlert> CombinedAlerts => AlertService.Alerts.Concat(_channelAlerts);

    public void Dispose()
    {
        try
        {
            AlertService.OnChange -= HandleAlertChange;
            Logger.Debug("Disposing channel subscription for {ChannelId}.", ChannelId);

            _disposable?.Dispose();
            Logger.Debug("Alert service subscription disposed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while disposing of the alert service subscription.");
        }
    }

    private void AddChannelAlert(PageAlert alert)
    {
        _channelAlerts.Add(alert);
        if (_channelAlerts.Count > MaxChannelAlerts)
        {
            _channelAlerts.RemoveAt(0);
        }

        _ = DebouncedStateHasChanged();
    }

    public void ClearChannelAlerts()
    {
        _channelAlerts.Clear();
        StateHasChanged();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AlertService.OnChange += HandleAlertChange;

        if (string.IsNullOrEmpty(ChannelId))
        {
            Logger.Error("ChannelId is null or empty during initialization.");
            return;
        }

        SubscribeToChannelNotifications(ChannelId);
        Logger.Debug("Alert service and channel notifications subscription initialized.");
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        Logger.Debug("Channel Id set to {ChannelId} for PageAlertContainer.", ChannelId);
    }

    private void HandleAlertChange(object? sender, EventArgs e)
    {
        _ = DebouncedStateHasChanged();
        Logger.Debug("Alert service state changed; UI update queued.");
    }

    private void SubscribeToChannelNotifications(string channelId)
    {
        try
        {
            if (string.IsNullOrEmpty(channelId))
            {
                Logger.Warning("Channel ID is null or empty. Skipping channel subscription.");
                return;
            }

            if (Serializer == null)
            {
                Logger.Error("Serializer is not available. Cannot subscribe to channel notifications.");
                return;
            }

            var bag = DisposableBag.CreateBuilder();

            ChannelNotificationSubscriber.Subscribe(channelId, Handler).AddTo(bag);
            Logger.Debug("Subscription created for channel: {ChannelId}", channelId);

            _disposable = bag.Build();
        }
        catch (Exception ex)
        {
            Logger.Error("Error subscribing to channel notifications: {Message}", ex.Message);
        }
    }

    private async void Handler(Notification notification)
    {
        try
        {
            if (notification.Type is not NotificationType.PageAlert)
            {
                return;
            }

            var pageAlert = new PageAlert(notification.Title, notification.Message,
                MapAlertType(notification.Severity));
            AddChannelAlert(pageAlert);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing notification for channel {ChannelId}", ChannelId);
        }
    }

    private void RemoveAlert(PageAlert alert)
    {
        if (AlertService.Alerts.Contains(alert))
        {
            AlertService.RemoveAlert(alert.Id);
        }
        else
        {
            _channelAlerts.Remove(alert);
            StateHasChanged();
        }
    }

    private async Task DebouncedStateHasChanged()
    {
        await DebounceService.DebounceAsync(
            () => InvokeAsync(StateHasChanged),
            "PageAlertContainerStateUpdate",
            _debounceTime
        );
    }

    /// <summary>
    ///     Maps NotificationSeverity to AlertType.
    /// </summary>
    /// <param name="severity">The severity of the notification.</param>
    /// <returns>The corresponding AlertType.</returns>
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
