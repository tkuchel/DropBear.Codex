using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     A Blazor component for displaying snackbar notifications.
///     Optimized for Blazor Server with proper thread safety and state management.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.Snackbar;

    private readonly SemaphoreSlim _snackbarSemaphore = new(1, 1);
    private volatile bool _isInitialized;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<DropBearSnackbar>? _dotNetRef;

    [Parameter]
    [EditorRequired]
    public SnackbarInstance SnackbarInstance { get; init; } = null!;

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    private string CssClasses => $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";

    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed) return;

        try
        {
            await _snackbarSemaphore.WaitAsync(ComponentToken);

            _jsModule = await GetJsModuleAsync(JsModuleName);

            // Create the snackbar in JS
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.createSnackbar",
                ComponentToken,
                SnackbarInstance.Id,
                SnackbarInstance
            );

            // Set up .NET reference for callbacks
            _dotNetRef = DotNetObjectReference.Create(this);
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.setDotNetReference",
                ComponentToken,
                SnackbarInstance.Id,
                _dotNetRef
            );

            // Show the snackbar
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.show",
                ComponentToken,
                SnackbarInstance.Id
            );

            // Start progress if auto-close enabled
            if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
            {
                await _jsModule.InvokeVoidAsync(
                    $"{JsModuleName}API.startProgress",
                    ComponentToken,
                    SnackbarInstance.Id,
                    SnackbarInstance.Duration
                );
            }

            _isInitialized = true;
            LogDebug("Snackbar initialized: {Id}", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize snackbar: {Id}", ex, SnackbarInstance.Id);
            throw;
        }
        finally
        {
            _snackbarSemaphore.Release();
        }
    }

    private async Task HandleActionClick(SnackbarAction action)
    {
        if (!_isInitialized || IsDisposed) return;

        try
        {
            if (action.OnClick != null)
            {
                await action.OnClick.Invoke();
            }
            await CloseAsync();
        }
        catch (Exception ex)
        {
            LogError("Action click failed: {Label}", ex, action.Label);
            await CloseAsync();
        }
    }

    private async Task CloseAsync()
    {
        if (!_isInitialized || IsDisposed || _jsModule == null) return;

        try
        {
            await _snackbarSemaphore.WaitAsync(ComponentToken);

            try
            {
                await _jsModule.InvokeVoidAsync(
                    $"{JsModuleName}API.hide",
                    ComponentToken,
                    SnackbarInstance.Id
                );
            }
            catch (Exception ex) when (IsElementRemovedError(ex))
            {
                LogWarning("Element already removed: {Id}", SnackbarInstance.Id);
            }

            if (OnClose.HasDelegate)
            {
                await InvokeAsync(async () => await OnClose.InvokeAsync());
            }

            LogDebug("Snackbar closed: {Id}", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to close snackbar: {Id}", ex, SnackbarInstance.Id);
        }
        finally
        {
            _snackbarSemaphore.Release();
        }
    }

    [JSInvokable]
    public async Task OnProgressComplete()
    {
        LogDebug("Progress complete: {Id}", SnackbarInstance.Id);
        await InvokeAsync(async () => await CloseAsync());
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_jsModule != null)
            {
                await _snackbarSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _jsModule.InvokeVoidAsync(
                        $"{JsModuleName}API.dispose",
                        cts.Token,
                        SnackbarInstance.Id
                    );
                    LogDebug("Snackbar resources cleaned up: {Id}", SnackbarInstance.Id);
                }
                catch (Exception ex) when (IsElementRemovedError(ex))
                {
                    LogWarning("Element already removed during cleanup: {Id}", SnackbarInstance.Id);
                }
                finally
                {
                    _snackbarSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup snackbar: {Id}", ex, SnackbarInstance.Id);
        }
        finally
        {
            try
            {
                _dotNetRef?.Dispose();
                _snackbarSemaphore.Dispose();
            }
            catch (ObjectDisposedException) { }

            _dotNetRef = null;
            _jsModule = null;
            _isInitialized = false;
        }
    }

    private static bool IsElementRemovedError(Exception? ex)
    {
        return ex is not null &&
               ex.Message.Contains("removeChild") &&
               ex.Message.Contains("parentNode is null");
    }
}
