#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearSnackbarNotificationContainer : DropBearComponentBase, IAsyncDisposable
{
    private const int MaxChannelSnackbars = 100;
    private const int StateUpdateDebounceMs = 100;
    private const int DomUpdateDelayMs = 50;

    private static readonly ILogger Logger = LoggerFactory.Logger
        .ForContext<DropBearSnackbarNotificationContainer>();

    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private IDisposable? _channelSubscription;
    private bool _isDisposed;

    private ConcurrentQueue<SnackbarInstance> _snackbars = new();

    [Parameter] public string ChannelId { get; set; } = string.Empty;

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Logger.Debug("Disposing SnackbarNotificationContainer...");

        try
        {
            await _disposalTokenSource.CancelAsync();
            UnsubscribeFromSnackbarEvents();
            _channelSubscription?.Dispose();
            await DisposeSnackbarsAsync(_snackbars.ToList());

            _updateLock.Dispose();
            _disposalTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during disposal");
            throw;
        }

        Logger.Debug("SnackbarNotificationContainer disposed.");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitializeSubscriptions();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (!string.IsNullOrEmpty(ChannelId))
        {
            Logger.Debug("Channel Id set to {ChannelId} for SnackbarContainer.", ChannelId);
        }
    }

    private void InitializeSubscriptions()
    {
        try
        {
            SubscribeToSnackbarEvents();
            if (!string.IsNullOrEmpty(ChannelId))
            {
                SubscribeToChannelNotifications(ChannelId);
            }

            Logger.Debug("SnackbarNotificationContainer initialized.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize subscriptions");
            throw;
        }
    }

    private void SubscribeToSnackbarEvents()
    {
        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbarsAsync;
        Logger.Debug("Subscribed to SnackbarService events.");
    }

    private void UnsubscribeFromSnackbarEvents()
    {
        if (SnackbarService != null)
        {
            SnackbarService.OnShow -= ShowSnackbarAsync;
            SnackbarService.OnHideAll -= HideAllSnackbarsAsync;
            Logger.Debug("Unsubscribed from SnackbarService events.");
        }
    }

    private void SubscribeToChannelNotifications(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            Logger.Warning("Channel ID is null or empty. Skipping channel subscription.");
            return;
        }

        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channelId, HandleNotification).AddTo(bag);
        _channelSubscription = bag.Build();
        Logger.Debug("Subscribed to channel notifications for {ChannelId}", channelId);
    }

    private ValueTask HandleNotification(Notification notification, CancellationToken token)
    {
        if (_isDisposed || notification.Type != NotificationType.Toast)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            var snackbarOptions = new SnackbarNotificationOptions(
                notification.Title ?? "Notification",
                notification.Message,
                MapSnackbarType(notification.Severity));

            var snackbar = new SnackbarNotificationEventArgs(snackbarOptions);
            _ = ShowSnackbarAsync(this, snackbar);

            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification");
            return ValueTask.CompletedTask;
        }
    }

    private async Task ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            if (_snackbars.Count >= MaxChannelSnackbars)
            {
                await RemoveOldestSnackbarAsync();
            }

            var snackbar = new SnackbarInstance(e.Options);
            _snackbars.Enqueue(snackbar);
            Logger.Debug("Snackbar added with ID: {SnackbarId}", snackbar.Id);

            await DebouncedStateUpdateAsync();

            if (snackbar.ComponentRef is not null)
            {
                await Task.Delay(DomUpdateDelayMs, _disposalTokenSource.Token);
                await snackbar.ComponentRef.ShowAsync();

                if (snackbar.Duration > 0)
                {
                    _ = AutoHideSnackbarAsync(snackbar);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error showing snackbar");
            throw;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task RemoveSnackbarAsync(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            var updatedSnackbars = new ConcurrentQueue<SnackbarInstance>(
                _snackbars.Where(s => s.Id != snackbar.Id)
            );

            if (snackbar.ComponentRef is not null)
            {
                await snackbar.ComponentRef.DismissAsync();
                Logger.Debug("Snackbar dismissed with ID: {SnackbarId}", snackbar.Id);
            }

            Interlocked.Exchange(ref _snackbars, updatedSnackbars);
            await DebouncedStateUpdateAsync();
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing snackbar: {SnackbarId}", snackbar.Id);
            throw;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task HideAllSnackbarsAsync(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        Logger.Debug("Hiding all snackbars...");
        await _updateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            var snackbars = _snackbars.ToList();
            var dismissTasks = snackbars
                .Where(s => s.ComponentRef != null)
                .Select(s => s.ComponentRef!.DismissAsync());

            await Task.WhenAll(dismissTasks);
            _snackbars.Clear();

            Logger.Debug("All snackbars hidden and cleared.");
            await DebouncedStateUpdateAsync();
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while hiding all snackbars.");
            throw;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task AutoHideSnackbarAsync(SnackbarInstance snackbar)
    {
        try
        {
            await Task.Delay(snackbar.Duration, _disposalTokenSource.Token);
            await RemoveSnackbarAsync(snackbar);
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error auto-hiding snackbar: {SnackbarId}", snackbar.Id);
        }
    }

    private async Task RemoveOldestSnackbarAsync()
    {
        if (_snackbars.TryDequeue(out var oldest))
        {
            await RemoveSnackbarAsync(oldest);
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
            "SnackbarContainerStateUpdate",
            TimeSpan.FromMilliseconds(StateUpdateDebounceMs)
        );
    }

    private async Task DisposeSnackbarsAsync(IEnumerable<SnackbarInstance> snackbars)
    {
        foreach (var snackbar in snackbars)
        {
            try
            {
                if (snackbar.ComponentRef is not null)
                {
                    await snackbar.ComponentRef.DisposeAsync();
                }
            }
            catch (JSDisconnectedException ex)
            {
                Logger.Warning(ex, "JSDisconnectedException occurred while disposing snackbar: {SnackbarId}",
                    snackbar.Id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error occurred while disposing snackbar: {SnackbarId}", snackbar.Id);
            }
        }
    }

    private async Task OnSnackbarAction(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (snackbar.OnAction is not null)
            {
                await snackbar.OnAction.Invoke();
                Logger.Debug("Snackbar action invoked for SnackbarId: {SnackbarId}", snackbar.Id);
            }

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling snackbar action: {SnackbarId}", snackbar.Id);
            throw;
        }
    }

    private static SnackbarType MapSnackbarType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Information => SnackbarType.Information,
            NotificationSeverity.Success => SnackbarType.Success,
            NotificationSeverity.Warning => SnackbarType.Warning,
            NotificationSeverity.Error or NotificationSeverity.Critical => SnackbarType.Error,
            NotificationSeverity.NotSpecified => SnackbarType.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown NotificationSeverity value")
        };
    }
}
