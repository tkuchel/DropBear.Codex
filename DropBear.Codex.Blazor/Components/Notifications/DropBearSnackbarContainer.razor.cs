#region

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
///     Manages and displays a collection of snackbars, subscribed to both a global channel and optional channel ID.
/// </summary>
public sealed partial class DropBearSnackbarContainer : DropBearComponentBase
{
    private const int MaxSnackbars = 5;

    private readonly List<SnackbarInstance> _activeSnackbars = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IDisposable? _channelSubscription;

    /// <summary>
    ///     An optional channel ID for targeted notifications.
    /// </summary>
    [Parameter]
    public string? ChannelId { get; set; }

    /// <summary>
    ///     The position on screen where snackbars will appear (e.g., BottomRight).
    /// </summary>
    [Parameter]
    public SnackbarPosition Position { get; set; } = SnackbarPosition.BottomRight;

    /// <summary>
    ///     Returns the CSS class corresponding to the chosen <see cref="SnackbarPosition" />.
    /// </summary>
    private string GetPositionClass()
    {
        return Position switch
        {
            SnackbarPosition.TopLeft => "top-left",
            SnackbarPosition.TopRight => "top-right",
            SnackbarPosition.BottomLeft => "bottom-left",
            _ => "bottom-right"
        };
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        try
        {
            SubscribeToSnackbarEvents();
            if (!string.IsNullOrEmpty(ChannelId))
            {
                SubscribeToChannelNotifications();
            }

            SubscribeToGlobalNotifications();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize DropBearSnackbarContainer");
            throw;
        }
    }

    /// <summary>
    ///     Subscribes to the <see cref="ISnackbarService" /> events for showing snackbars.
    /// </summary>
    private void SubscribeToSnackbarEvents()
    {
        if (SnackbarService is null)
        {
            Logger.Warning("SnackbarService is null, cannot subscribe to events");
            return;
        }

        SnackbarService.OnShow += ShowSnackbar;
        Logger.Debug("Subscribed to SnackbarService OnShow event.");
    }

    /// <summary>
    ///     Unsubscribes from the <see cref="ISnackbarService" /> events.
    /// </summary>
    private void UnsubscribeFromSnackbarEvents()
    {
        if (SnackbarService is not null)
        {
            SnackbarService.OnShow -= ShowSnackbar;
            Logger.Debug("Unsubscribed from SnackbarService OnShow event.");
        }
    }

    /// <summary>
    ///     Subscribes to channel-specific notifications if <see cref="ChannelId" /> is provided.
    /// </summary>
    private void SubscribeToChannelNotifications()
    {
        var channel = $"{GlobalConstants.UserNotificationChannel}.{ChannelId}";
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channel, HandleNotificationAsync).AddTo(bag);
        _channelSubscription = bag.Build();

        Logger.Debug("Subscribed to channel notifications for {ChannelId}", ChannelId);
    }

    /// <summary>
    ///     Subscribes to global notifications for showing snackbars.
    /// </summary>
    private void SubscribeToGlobalNotifications()
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync)
            .AddTo(bag);

        _channelSubscription = bag.Build();
        Logger.Debug("Subscribed to global notifications.");
    }

    /// <summary>
    ///     Handles incoming notifications of <see cref="NotificationType.Toast" />.
    ///     Converts them to <see cref="SnackbarInstance" /> objects and shows them.
    /// </summary>
    private async ValueTask HandleNotificationAsync(Notification notification, CancellationToken token)
    {
        if (notification.Type != NotificationType.Toast)
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
                Duration = GetDurationForSeverity(notification.Severity)
            };

            await ShowSnackbar(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling notification as snackbar.");
        }
    }

    /// <summary>
    ///     Maps a <see cref="NotificationSeverity" /> to a <see cref="SnackbarType" />.
    /// </summary>
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
    ///     Returns the duration (in ms) for auto-dismiss, based on the severity.
    ///     Zero means manual dismissal required.
    /// </summary>
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
    ///     Adds a new snackbar, removing the oldest if the maximum count is exceeded.
    ///     Then triggers JS to display it.
    /// </summary>
    private async Task ShowSnackbar(SnackbarInstance snackbar)
    {
        try
        {
            await _semaphore.WaitAsync();

            while (_activeSnackbars.Count >= MaxSnackbars)
            {
                var oldestId = _activeSnackbars[0].Id;
                await RemoveSnackbar(oldestId);
            }

            _activeSnackbars.Add(snackbar);
            await InvokeAsync(StateHasChanged);

            try
            {
                // Allow a short delay for rendering
                await Task.Delay(50);

                await SafeJsVoidInteropAsync("DropBearSnackbar.show", snackbar.Id);

                // If auto-dismiss (Duration > 0), start progress
                if (snackbar is { RequiresManualClose: false, Duration: > 0 })
                {
                    await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress", snackbar.Id, snackbar.Duration);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing snackbar: {Id}", snackbar.Id);

                // If something goes wrong, remove the snackbar
                _activeSnackbars.Remove(snackbar);
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Removes a snackbar by ID, hiding it in JS first, then removing it from the list.
    /// </summary>
    private async Task RemoveSnackbar(string id)
    {
        try
        {
            await _semaphore.WaitAsync();

            var snackbar = _activeSnackbars.FirstOrDefault(s => s.Id == id);
            if (snackbar != null)
            {
                try
                {
                    await SafeJsVoidInteropAsync("DropBearSnackbar.hide", id);
                    await Task.Delay(300); // Wait for CSS animation
                    _activeSnackbars.Remove(snackbar);
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error removing snackbar: {Id}", id);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            // Initialize the container in JS if needed
            await SafeJsVoidInteropAsync("DropBearSnackbar.initialize", ComponentId);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                UnsubscribeFromSnackbarEvents();
                _channelSubscription?.Dispose();

                // Dispose each active snackbar in JS
                foreach (var snackbar in _activeSnackbars.ToList())
                {
                    try
                    {
                        await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", snackbar.Id);
                        await Task.Delay(50); // small delay
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error disposing snackbar: {Id}", snackbar.Id);
                    }
                }

                _activeSnackbars.Clear();
                _semaphore.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during snackbar container disposal");
            }
        }

        await base.DisposeAsync(disposing);
    }
}
