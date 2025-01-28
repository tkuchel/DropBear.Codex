#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly SemaphoreSlim _jsLock = new(1, 1);
    private bool _isDisposed;
    private bool _isInitialized;

    [Inject] private new ILogger<DropBearSnackbar> Logger { get; set; } = null!;

    [Parameter] [EditorRequired] public SnackbarInstance SnackbarInstance { get; init; } = null!;

    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    private string CssClasses => $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || _isDisposed || _isInitialized)
        {
            return;
        }

        try
        {
            await InitializeSnackbarAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize snackbar {SnackbarId}", SnackbarInstance.Id);
        }
    }

    private async Task InitializeSnackbarAsync()
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await _jsLock.WaitAsync();

                // Ensure JS module is initialized
                await EnsureJsModuleInitializedAsync("DropBearSnackbar");

                // Create and show the snackbar
                await SafeJsVoidInteropAsync("DropBearSnackbar.createSnackbar", SnackbarInstance.Id);
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", SnackbarInstance.Id);

                // If auto-dismiss is enabled, start the progress
                if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
                {
                    await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress",
                        SnackbarInstance.Id, SnackbarInstance.Duration);
                }

                Logger.LogDebug("Snackbar {SnackbarId} initialized and shown", SnackbarInstance.Id);
                break;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Logger.LogWarning(ex,
                    "Attempt {Attempt} failed to initialize snackbar {SnackbarId}. Retrying in {Delay}ms",
                    attempt, SnackbarInstance.Id, RetryDelayMs);
                await Task.Delay(RetryDelayMs, _disposalTokenSource.Token);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize snackbar {SnackbarId} after {MaxRetries} attempts",
                    SnackbarInstance.Id, MaxRetries);
                throw;
            }
            finally
            {
                _jsLock.Release();
            }
        }
    }

    private async Task HandleActionClick(SnackbarAction action)
    {
        if (action.OnClick == null)
        {
            await Close();
            return;
        }

        try
        {
            await action.OnClick.Invoke();
            await Close();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing action '{ActionLabel}' for snackbar {SnackbarId}",
                action.Label, SnackbarInstance.Id);
        }
    }

    private async Task Close()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _jsLock.WaitAsync();
            await SafeJsVoidInteropAsync("DropBearSnackbar.hide", SnackbarInstance.Id);
            await Task.Delay(300, _disposalTokenSource.Token); // Animation duration
            await OnClose.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing snackbar {SnackbarId}", SnackbarInstance.Id);
        }
        finally
        {
            _jsLock.Release();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            await _jsLock.WaitAsync();
            await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", SnackbarInstance.Id);
            await _disposalTokenSource.CancelAsync();
            _disposalTokenSource.Dispose();
            _jsLock.Dispose();
            Logger.LogDebug("Snackbar {SnackbarId} disposed", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing snackbar {SnackbarId}", SnackbarInstance.Id);
        }
        finally
        {
            _jsLock.Release();
        }

        await base.DisposeAsync();
    }
}
