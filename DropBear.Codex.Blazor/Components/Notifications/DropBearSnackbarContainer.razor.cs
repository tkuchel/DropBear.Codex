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

    private void UnsubscribeFromSnackbarEvents()
    {
        if (SnackbarService is not null)
        {
            SnackbarService.OnShow -= ShowSnackbar;
            Logger.Debug("Unsubscribed from SnackbarService OnShow event.");
        }
    }

    private void SubscribeToChannelNotifications()
    {
        var channel = $"{GlobalConstants.UserNotificationChannel}.{ChannelId}";
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber.Subscribe(channel, HandleNotificationAsync).AddTo(bag);
        _channelSubscription = bag.Build();

        Logger.Debug("Subscribed to channel notifications for {ChannelId}", ChannelId);
    }

    private void SubscribeToGlobalNotifications()
    {
        var bag = DisposableBag.CreateBuilder();
        NotificationSubscriber
            .Subscribe(GlobalConstants.GlobalNotificationChannel, HandleNotificationAsync)
            .AddTo(bag);

        _channelSubscription = bag.Build();
        Logger.Debug("Subscribed to global notifications.");
    }

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
    ///     Then triggers JS to create, show, and optionally start progress.
    /// </summary>
    private async Task ShowSnackbar(SnackbarInstance snackbar)
    {
        try
        {
            await _semaphore.WaitAsync();

            // If we're at capacity, remove the oldest
            while (_activeSnackbars.Count >= MaxSnackbars)
            {
                var oldestId = _activeSnackbars[0].Id;
                await RemoveSnackbar(oldestId);
            }

            _activeSnackbars.Add(snackbar);
            await InvokeAsync(StateHasChanged);

            try
            {
                //  Ensure the JS module is initialized
                await EnsureJsModuleInitializedAsync("DropBearSnackbar");

                // 1) Create the snackbar manager in JS for this ID
                await SafeJsVoidInteropAsync("DropBearSnackbar.createSnackbar", snackbar.Id);

                // 2) Show the snackbar
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", snackbar.Id);

                // 3) If auto-dismiss, start the progress
                if (snackbar is { RequiresManualClose: false, Duration: > 0 })
                {
                    await SafeJsVoidInteropAsync(
                        "DropBearSnackbar.startProgress",
                        snackbar.Id,
                        snackbar.Duration
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing snackbar: {Id}", snackbar.Id);

                // If something fails, remove the snackbar from the list
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
    ///     Removes a snackbar by ID: calls 'hide' in JS, waits for animation, then removes from the list.
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
                    await EnsureJsModuleInitializedAsync("DropBearSnackbar");

                    // Hide in JS
                    await SafeJsVoidInteropAsync("DropBearSnackbar.hide", id);

                    // Wait for CSS animation (300ms default)
                    await Task.Delay(300);

                    // Remove from active list
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

    /// <summary>
    ///     On first render, call 'DropBearSnackbar.initialize()' (no parameters now).
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            // Global no-arg init for the DropBearSnackbar module - No longer needed as the module will get initialized on DOMContentLoaded
            // await EnsureJsModuleInitializedAsync("DropBearSnackbar");
            // await SafeJsVoidInteropAsync("DropBearSnackbar.initialize");
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
                        await EnsureJsModuleInitializedAsync("DropBearSnackbar");
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
