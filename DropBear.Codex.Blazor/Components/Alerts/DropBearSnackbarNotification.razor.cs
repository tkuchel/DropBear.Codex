#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearSnackbarNotification : DropBearComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger
        .ForContext<DropBearSnackbarNotification>();

    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly string _snackbarId = $"snackbar-{Guid.NewGuid()}";

    private readonly SemaphoreSlim _stateUpdateLock = new(1, 1);
    private bool _isAnimating;

    private bool _isDismissed;
    private bool _isDisposed;



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

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await _disposalTokenSource.CancelAsync();

            if (!_isDismissed && IsVisible)
            {
                await DismissAsync();
            }

            await JsRuntime.InvokeVoidAsync(
                "DropBearSnackbar.disposeSnackbar",
                CancellationToken.None,
                _snackbarId
            );

            _stateUpdateLock.Dispose();
            _disposalTokenSource.Dispose();

            Logger.Debug("Snackbar disposed: {SnackbarId}", _snackbarId);
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "JS error during disposal: {SnackbarId}", _snackbarId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing snackbar: {SnackbarId}", _snackbarId);
            throw;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsVisible)
        {
            await ShowAsync();
        }
    }

    public async Task ShowAsync()
    {
        if (_isDisposed || _isDismissed || _isAnimating || IsVisible)
        {
            Logger.Warning(
                "Invalid state for showing snackbar: {SnackbarId}. Disposed: {Disposed}, Dismissed: {Dismissed}, Animating: {Animating}, Visible: {Visible}",
                _snackbarId, _isDisposed, _isDismissed, _isAnimating, IsVisible);
            return;
        }

        await _stateUpdateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            _isAnimating = true;
            IsVisible = true;
            await InvokeAsync(StateHasChanged);

            // Ensure DOM update and animation completion
            await Task.Delay(50, _disposalTokenSource.Token);
            await InitializeProgressBarAsync();

            _isAnimating = false;
            Logger.Debug("Snackbar shown successfully: {SnackbarId}", _snackbarId);
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "JS error showing snackbar: {SnackbarId}", _snackbarId);
            await HandleJsError(ex);
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

    public async Task DismissAsync()
    {
        if (_isDisposed || _isDismissed || _isAnimating || !IsVisible)
        {
            return;
        }

        await _stateUpdateLock.WaitAsync(_disposalTokenSource.Token);
        try
        {
            _isAnimating = true;
            await HideSnackbarAsync();

            IsVisible = false;
            _isDismissed = true;
            _isAnimating = false;

            await InvokeAsync(StateHasChanged);
            Logger.Debug("Snackbar dismissed: {SnackbarId}", _snackbarId);
        }
        catch (OperationCanceledException)
        {
            // Disposal in progress, ignore
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "JS error dismissing snackbar: {SnackbarId}", _snackbarId);
            await HandleJsError(ex);
        }
        finally
        {
            _stateUpdateLock.Release();
        }
    }

    private async Task InitializeProgressBarAsync()
    {
        try
        {
            if (Duration > 0)
            {
                await JsRuntime.InvokeVoidAsync(
                    "DropBearSnackbar.startProgress",
                    _disposalTokenSource.Token,
                    _snackbarId,
                    Duration
                );
            }
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "Error initializing progress bar: {SnackbarId}", _snackbarId);
            throw;
        }
    }

    private async Task HideSnackbarAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "DropBearSnackbar.hideSnackbar",
                _disposalTokenSource.Token,
                _snackbarId
            );
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "Error hiding snackbar: {SnackbarId}", _snackbarId);
            throw;
        }
    }

    private async Task HandleJsError(JSException ex)
    {
        // Attempt graceful degradation
        IsVisible = false;
        _isDismissed = true;
        await InvokeAsync(StateHasChanged);
        throw new SnackbarException("JS interaction failed", ex);
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
        if (_isDisposed || _isDismissed || _isAnimating)
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
}
