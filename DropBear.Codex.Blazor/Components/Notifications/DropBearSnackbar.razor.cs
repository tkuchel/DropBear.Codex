#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;
    private readonly TaskCompletionSource _animationComplete = new();
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly DotNetObjectReference<DropBearSnackbar>? _dotNetRef;
    private readonly SemaphoreSlim _jsLock = new(1, 1);
    private bool _isClosing;
    private bool _isDisposed;
    private bool _isInitialized;

    public DropBearSnackbar()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
    }

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
            Logger.LogInformation("Initializing snackbar {Id}", SnackbarInstance.Id);
            await InitializeSnackbarAsync();

            if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
            {
                Logger.LogInformation("Setting up auto-close timer for snackbar {Id} with duration {Duration}ms",
                    SnackbarInstance.Id, SnackbarInstance.Duration);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(SnackbarInstance.Duration, _disposalTokenSource.Token);
                        Logger.LogInformation("Auto-close timer completed for snackbar {Id}", SnackbarInstance.Id);
                        await InvokeAsync(Close);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogInformation("Auto-close timer cancelled for snackbar {Id}", SnackbarInstance.Id);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error in auto-close timer for snackbar {Id}", SnackbarInstance.Id);
                    }
                });
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize snackbar {Id}", SnackbarInstance.Id);
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

                Logger.LogDebug("Creating snackbar {Id}", SnackbarInstance.Id);

                // Create and show the snackbar
                await SafeJsVoidInteropAsync("DropBearSnackbar.createSnackbar", SnackbarInstance.Id);

                Logger.LogDebug("Setting .NET reference for snackbar {Id}", SnackbarInstance.Id);

                // Set the .NET reference
                await SafeJsVoidInteropAsync("DropBearSnackbar.setDotNetReference",
                    SnackbarInstance.Id,
                    DotNetObjectReference.Create(this));

                Logger.LogDebug("Showing snackbar {Id}", SnackbarInstance.Id);
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", SnackbarInstance.Id);

                if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
                {
                    Logger.LogDebug("Starting progress for snackbar {Id} with duration {Duration}",
                        SnackbarInstance.Id, SnackbarInstance.Duration);
                    await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress",
                        SnackbarInstance.Id, SnackbarInstance.Duration);
                }

                Logger.LogDebug("Snackbar {Id} initialized and shown", SnackbarInstance.Id);
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

    [JSInvokable]
    public async Task OnProgressComplete()
    {
        try
        {
            Logger.LogInformation("Progress complete for snackbar {Id}", SnackbarInstance.Id);
            await Close();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling progress complete for snackbar {Id}", SnackbarInstance.Id);
        }
    }

    private async Task Close()
    {
        if (_isDisposed || _isClosing)
        {
            Logger.LogWarning("Close called but snackbar {Id} is disposed: {Disposed} or closing: {Closing}",
                SnackbarInstance.Id, _isDisposed, _isClosing);
            return;
        }

        try
        {
            _isClosing = true;
            await _jsLock.WaitAsync();

            Logger.LogDebug("Starting close sequence for snackbar {SnackbarId}", SnackbarInstance.Id);

            // Hide in JS
            await SafeJsVoidInteropAsync("DropBearSnackbar.hide", SnackbarInstance.Id);

            // Wait for animation
            await Task.Delay(300, _disposalTokenSource.Token);

            // Notify parent and wait for it to complete
            if (OnClose.HasDelegate)
            {
                await OnClose.InvokeAsync();
                Logger.LogInformation("OnClose completed for snackbar {Id}", SnackbarInstance.Id);
            }
            else
            {
                Logger.LogWarning("OnClose has no delegates for snackbar {Id}", SnackbarInstance.Id);
            }

            Logger.LogDebug("Close sequence completed for snackbar {SnackbarId}", SnackbarInstance.Id);
            _animationComplete.TrySetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing snackbar {SnackbarId}", SnackbarInstance.Id);
            _animationComplete.TrySetException(ex);
        }
        finally
        {
            _jsLock.Release();
        }
    }


    public override async ValueTask DisposeAsync()
    {
        try
        {
            _dotNetRef?.Dispose();

            await base.DisposeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing snackbar {Id}", SnackbarInstance.Id);
        }
    }
}
