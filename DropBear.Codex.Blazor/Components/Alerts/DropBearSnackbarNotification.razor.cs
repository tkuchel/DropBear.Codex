#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearSnackbarNotification : DropBearComponentBase
{
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly SemaphoreSlim _stateUpdateLock = new(1, 1);
    private bool _isAnimating;
    private bool _isDismissed;
    private string _snackbarId => $"snackbar-{ComponentId}";

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public SnackbarType Type { get; set; } = SnackbarType.Information;
    [Parameter] public int Duration { get; set; } = 5000;
    [Parameter] public bool IsDismissible { get; set; } = true;
    [Parameter] public string ActionText { get; set; } = "Dismiss";
    [Parameter] public EventCallback OnAction { get; set; }
    [Parameter] public bool IsVisible { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsVisible)
        {
            await ShowAsync();
        }
    }

    public async Task ShowAsync()
    {
        if (IsDisposed || _isDismissed || _isAnimating || IsVisible)
        {
            Logger.Warning(
                "Invalid state for showing snackbar: {SnackbarId}. Disposed: {Disposed}, Dismissed: {Dismissed}, Animating: {Animating}, Visible: {Visible}",
                _snackbarId, IsDisposed, _isDismissed, _isAnimating, IsVisible);
            return;
        }

        await _stateUpdateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            await WaitForJsInitializationAsync();

            _isAnimating = true;
            IsVisible = true;
            await InvokeStateHasChangedAsync(async () =>
            {
                // Ensure DOM update and animation completion
                await Task.Delay(50, _disposalTokenSource.Token);
                await InitializeProgressBarAsync();
            });

            _isAnimating = false;
            Logger.Debug("Snackbar shown successfully: {SnackbarId}", _snackbarId);
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (JSDisconnectedException ex)
        {
            Logger.Warning(ex, "Circuit disconnected while showing snackbar: {SnackbarId}", _snackbarId);
            await HandleJsError();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error showing snackbar: {SnackbarId}", _snackbarId);
            throw new SnackbarException("Error showing snackbar", ex);
        }
        finally
        {
            _stateUpdateLock.Release();
        }
    }

    private async Task WaitForJsInitializationAsync()
    {
        // Try multiple times with increasing delays
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var isLoaded = await SafeJsInteropAsync<bool>(
                    "eval",
                    "typeof window.DropBearSnackbar !== 'undefined' && typeof window.DropBearSnackbar.startProgress === 'function'"
                );

                if (isLoaded)
                {
                    return;
                }

                await Task.Delay(100 * (i + 1), _disposalTokenSource.Token);
            }
            catch (Exception) when (i < 2)
            {
                // Continue trying unless it's the last attempt
            }
        }

        throw new SnackbarException("DropBearSnackbar failed to initialize after multiple attempts");
    }

    public async Task DismissAsync()
    {
        if (IsDisposed || _isDismissed || _isAnimating || !IsVisible)
        {
            return;
        }

        await _stateUpdateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            _isAnimating = true;
            await HideSnackbarAsync();

            await InvokeStateHasChangedAsync(() =>
            {
                IsVisible = false;
                _isDismissed = true;
                _isAnimating = false;
                return Task.CompletedTask;
            });

            Logger.Debug("Snackbar dismissed: {SnackbarId}", _snackbarId);
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (JSDisconnectedException ex)
        {
            Logger.Warning(ex, "Circuit disconnected while dismissing snackbar: {SnackbarId}", _snackbarId);
            await HandleJsError();
        }
        finally
        {
            _stateUpdateLock.Release();
        }
    }

    private async Task InitializeProgressBarAsync()
    {
        if (Duration <= 0)
        {
            return;
        }

        await SafeJsInteropAsync<bool>("eval",
            "typeof window.DropBearSnackbar !== 'undefined' && typeof window.DropBearSnackbar.startProgress === 'function'");

        await SafeJsVoidInteropAsync(
            "DropBearSnackbar.startProgress",
            _snackbarId,
            Duration
        );
    }

    private async Task HideSnackbarAsync()
    {
        await SafeJsVoidInteropAsync(
            "DropBearSnackbar.hideSnackbar",
            _snackbarId
        );
    }

    private async Task HandleJsError()
    {
        // Graceful degradation
        await InvokeStateHasChangedAsync(() =>
        {
            IsVisible = false;
            _isDismissed = true;
            return Task.CompletedTask;
        });
    }

    private static string GetIconClass(SnackbarType type)
    {
        return type switch
        {
            SnackbarType.Information => "fas fa-info-circle",
            SnackbarType.Success => "fas fa-check-circle",
            SnackbarType.Warning => "fas fa-exclamation-triangle",
            SnackbarType.Error => "fas fa-times-circle",
            _ => "fas fa-info-circle"
        };
    }

    private static string GetSnackbarClasses(SnackbarType type)
    {
        return $"snackbar-{type.ToString().ToLowerInvariant()}";
    }

    private async Task OnActionClick()
    {
        if (IsDisposed || _isDismissed || _isAnimating)
        {
            return;
        }

        try
        {
            await OnAction.InvokeAsync();
            await DismissAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling action click: {SnackbarId}", _snackbarId);
            throw;
        }
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (!_isDismissed && IsVisible)
            {
                await DismissAsync();
            }

            // Cleanup events if available
            await SafeJsVoidInteropAsync(
                "eval",
                "if (window.EventEmitter) { window.EventEmitter.emit('snackbar:cleanup', '" + _snackbarId + "'); }"
            );

            await SafeJsVoidInteropAsync(
                "DropBearSnackbar.disposeSnackbar",
                _snackbarId
            );

            await _disposalTokenSource.CancelAsync();
            _stateUpdateLock.Dispose();
            _disposalTokenSource.Dispose();

            Logger.Debug("Snackbar cleaned up: {SnackbarId}", _snackbarId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during snackbar cleanup: {SnackbarId}", _snackbarId);
        }
    }
}
