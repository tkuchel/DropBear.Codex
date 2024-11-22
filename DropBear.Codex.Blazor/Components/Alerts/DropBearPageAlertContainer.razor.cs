#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlertContainer : DropBearComponentBase, IDisposable
{
    private const int MaxChannelAlerts = 100;
    private const int StateUpdateDebounceMs = 100;

    private static readonly ILogger Logger = LoggerFactory.Logger
        .ForContext<DropBearPageAlertContainer>();

    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly ConcurrentDictionary<Guid, PageAlert> _localAlerts = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private ConcurrentQueue<PageAlert> _channelAlerts = new();
    private IDisposable? _channelSubscription;
    private bool _isDisposed;
    private bool _isSubscribed;

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    private IEnumerable<PageAlert> CombinedAlerts
    {
        get
        {
            if (!AlertChannelManager.IsValidChannel(ChannelId))
            {
                return Enumerable.Empty<PageAlert>();
            }

            return AlertService.Alerts
                .Concat(_localAlerts.Values)
                .DistinctBy(a => a.Id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(MaxChannelAlerts);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _disposalTokenSource.Cancel();
            UnsubscribeFromAlerts();

            _updateLock.Dispose();
            _disposalTokenSource.Dispose();

            Logger.Debug("Disposed alert container for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing alert container");
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitializeContainer();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (!string.IsNullOrEmpty(ChannelId) && AlertChannelManager.IsValidChannel(ChannelId) && !_isSubscribed)
        {
            InitializeContainer();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(ChannelId) && AlertChannelManager.IsValidChannel(ChannelId))
        {
            await InitializeContainerAsync();
        }
    }

    private async Task InitializeContainerAsync()
    {
        if (string.IsNullOrEmpty(ChannelId))
        {
            Logger.Error("ChannelId is null or empty during initialization.");
            return;
        }

        try
        {
            await SubscribeToAlertsAsync();
            Logger.Debug("Alert container initialized for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize alert container for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private async Task SubscribeToAlertsAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        UnsubscribeFromAlerts();

        try
        {
            AlertService.OnAddAlert += HandleAddAlert;
            AlertService.OnRemoveAlert += HandleRemoveAlert;
            AlertService.OnClearAlerts += HandleClearAlerts;

            _channelSubscription = await SubscribeToChannelNotificationsAsync(ChannelId);
            _isSubscribed = true;

            Logger.Debug("Subscribed to alerts for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to alerts for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private async Task<IDisposable> SubscribeToChannelNotificationsAsync(string channelId)
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe<string, Notification>(channelId, HandleNotification)
            .AddTo(bag);
        return bag.Build();
    }

    private void InitializeContainer()
    {
        if (string.IsNullOrEmpty(ChannelId))
        {
            Logger.Error("ChannelId is null or empty during initialization.");
            return;
        }

        try
        {
            SubscribeToAlerts();
            Logger.Debug("Alert container initialized for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize alert container for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private void SubscribeToAlerts()
    {
        if (_isDisposed)
        {
            return;
        }

        UnsubscribeFromAlerts();

        try
        {
            AlertService.OnAddAlert += HandleAddAlert;
            AlertService.OnRemoveAlert += HandleRemoveAlert;
            AlertService.OnClearAlerts += HandleClearAlerts;

            _channelSubscription = SubscribeToChannelNotifications(ChannelId);
            _isSubscribed = true;

            Logger.Debug("Subscribed to alerts for channel: {ChannelId}", ChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to alerts for channel: {ChannelId}", ChannelId);
            throw;
        }
    }

    private void UnsubscribeFromAlerts()
    {
        if (_isSubscribed)
        {
            AlertService.OnAddAlert -= HandleAddAlert;
            AlertService.OnRemoveAlert -= HandleRemoveAlert;
            AlertService.OnClearAlerts -= HandleClearAlerts;
            _channelSubscription?.Dispose();

            Logger.Debug("Unsubscribed from alerts for channel: {ChannelId}", ChannelId);
            _isSubscribed = false;
        }
    }

    private async Task HandleAddAlert(object sender, PageAlertEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        await AddAlertAsync(e.Alert);
    }

    private async Task HandleRemoveAlert(object sender, PageAlertEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        await RemoveAlertAsync(e.Alert);
    }

    private async Task HandleClearAlerts(object sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        await ClearAlertsAsync();
    }

    private IDisposable SubscribeToChannelNotifications(string channelId)
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe<string, Notification>(channelId, HandleNotification)
            .AddTo(bag);
        return bag.Build();
    }

    private async ValueTask HandleNotification(Notification notification, CancellationToken token)
    {
        if (_isDisposed || notification.Type != NotificationType.PageAlert)
        {
            return;
        }

        var pageAlert = new PageAlert(
            notification.Title ?? "Alert",
            notification.Message,
            MapAlertType(notification.Severity)
        );

        await AddAlertAsync(pageAlert);
    }

    private async Task AddAlertAsync(PageAlert alert)
    {
        if (_isDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            while (_channelAlerts.Count >= MaxChannelAlerts)
            {
                _channelAlerts.TryDequeue(out _);
            }

            _channelAlerts.Enqueue(alert);
            Logger.Debug("Added alert: Type={AlertType}, Title='{AlertTitle}'",
                alert.Type, alert.Title);

            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task RemoveAlertAsync(PageAlert alert)
    {
        if (_isDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            var updatedAlerts = new ConcurrentQueue<PageAlert>(
                _channelAlerts.Where(a => a.Id != alert.Id)
            );

            Interlocked.Exchange(ref _channelAlerts, updatedAlerts);
            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task ClearAlertsAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            _channelAlerts.Clear();
            await DebouncedStateUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task DebouncedStateUpdateAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await DebounceService.DebounceAsync(
            async () =>
            {
                if (!_isDisposed)
                {
                    await InvokeAsync(StateHasChanged);
                }
            },
            "PageAlertContainerUpdate",
            TimeSpan.FromMilliseconds(StateUpdateDebounceMs)
        );
    }

    private static AlertType MapAlertType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => AlertType.Information,
            NotificationSeverity.Success => AlertType.Success,
            NotificationSeverity.Warning => AlertType.Warning,
            NotificationSeverity.Error or NotificationSeverity.Critical => AlertType.Danger,
            NotificationSeverity.NotSpecified => AlertType.Notification,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
    }

    private async Task OnCloseAlert(PageAlert alert)
    {
        if (_isDisposed)
        {
            return;
        }

        await RemoveAlertAsync(alert);
    }
}
