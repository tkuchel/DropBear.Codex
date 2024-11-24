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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Logger.Debug("SnackbarNotification initialized with ID: {Id}", _snackbarId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Logger.Debug("SnackbarNotification first render complete: {Id}", _snackbarId);
            await CheckDomElementAsync();
        }

        if (IsVisible)
        {
            Logger.Debug("SnackbarNotification rendered with visibility: {IsVisible}, ID: {Id}",
                IsVisible, _snackbarId);
            await CheckDomElementAsync();
        }
    }

    private async Task CheckDomElementAsync()
    {
        try
        {
            var exists = await SafeJsInteropAsync<bool>("eval",
                $"document.getElementById('{_snackbarId}') !== null");

            Logger.Debug("DOM element check for {Id}: {Exists}", _snackbarId, exists);

            if (exists)
            {
                var rect = await SafeJsInteropAsync<string>("eval",
                    $"JSON.stringify(document.getElementById('{_snackbarId}').getBoundingClientRect())");
                Logger.Debug("Element position: {Rect}", rect);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error checking DOM element");
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
            Logger.Debug("Starting show sequence for snackbar: {Id}", _snackbarId);
            await WaitForJsInitializationAsync();

            _isAnimating = true;
            IsVisible = true;

            await InvokeStateHasChangedAsync(async () =>
            {
                Logger.Debug("State updated, waiting for DOM update");
                await Task.Delay(50, _disposalTokenSource.Token);

                var exists = await SafeJsInteropAsync<bool>("eval",
                    $"document.getElementById('{_snackbarId}') !== null");
                Logger.Debug("Post-state-change DOM check: {Exists}", exists);

                if (exists)
                {
                    await InitializeProgressBarAsync();
                }
                else
                {
                    Logger.Warning("DOM element not found after state change");
                }
            });

            _isAnimating = false;
            Logger.Debug("Snackbar show sequence completed: {Id}", _snackbarId);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Show operation cancelled: {Id}", _snackbarId);
            throw;
        }
        catch (JSDisconnectedException ex)
        {
            Logger.Warning(ex, "Circuit disconnected while showing snackbar: {Id}", _snackbarId);
            await HandleJsError();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error showing snackbar: {Id}", _snackbarId);
            throw;
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

        try
        {
            Logger.Debug("Initializing progress bar for: {Id}", _snackbarId);
            var progressBar = await SafeJsInteropAsync<bool>("eval",
                $"document.querySelector('#{_snackbarId} .snackbar-progress') !== null");

            if (!progressBar)
            {
                Logger.Warning("Progress bar element not found: {Id}", _snackbarId);
                return;
            }

            await SafeJsVoidInteropAsync(
                "DropBearSnackbar.startProgress",
                _snackbarId,
                Duration
            );

            Logger.Debug("Progress bar initialized: {Id}", _snackbarId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing progress bar: {Id}", _snackbarId);
            throw;
        }
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
