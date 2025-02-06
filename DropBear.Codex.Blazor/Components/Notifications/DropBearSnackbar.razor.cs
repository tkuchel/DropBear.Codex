#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     A simplified Blazor component for displaying a snackbar.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.Snackbar;

    // Store the DotNet reference for cleanup.
    private DotNetObjectReference<DropBearSnackbar>? _dotNetRef;
    private IJSObjectReference? _jsModule;

    /// <summary>
    ///     Unique snackbar instance details (title, message, duration, etc.)
    /// </summary>
    [Parameter]
    [EditorRequired]
    public SnackbarInstance SnackbarInstance { get; init; } = null!;

    /// <summary>
    ///     Invoked when the snackbar closes.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Additional attributes for the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    /// <summary>
    ///     Convenience property for building CSS classes.
    /// </summary>
    private string CssClasses => $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";

    /// <inheritdoc />
    /// <remarks>
    ///     Loads the JS module on first render, then calls a matching "initialize"
    ///     method, passing the unique ID and a .NET reference.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // 1) Retrieve the module reference once
            _jsModule = await GetJsModuleAsync(JsModuleName);

            if (_jsModule is not null)
            {
                // 2) Create the snackbar in JS
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.createSnackbar", SnackbarInstance.Id, SnackbarInstance);

                // 3) Create and store the .NET reference for interop
                _dotNetRef = DotNetObjectReference.Create(this);
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.setDotNetReference", SnackbarInstance.Id, _dotNetRef);

                // 4) Show the snackbar
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.show", SnackbarInstance.Id);

                // 5) If auto-close is enabled, start the progress
                if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
                {
                    await _jsModule.InvokeVoidAsync($"{JsModuleName}API.startProgress", SnackbarInstance.Id, SnackbarInstance.Duration);
                }
            }

            LogDebug("Snackbar {Id} initialized via JS module.", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize DropBearSnackbar {Id}", ex, SnackbarInstance.Id);
        }
    }

    /// <summary>
    ///     (Optional) Provide a method for an action button within the snackbar
    ///     that, when clicked, triggers custom logic or closes the snackbar.
    /// </summary>
    private async Task HandleActionClick(SnackbarAction action)
    {
        if (action.OnClick != null)
        {
            try
            {
                await action.OnClick.Invoke();
            }
            catch (Exception ex)
            {
                LogError("Error in action OnClick: {Label}", ex, action.Label);
            }
        }

        await CloseAsync();
    }

    /// <summary>
    ///     .NET method to hide the snackbar, triggered from .NET or JS.
    /// </summary>
    private async Task CloseAsync()
    {
        if (IsDisposed || _jsModule is null)
        {
            return;
        }

        try
        {
            // Wrap the call to hide in a try/catch to specifically ignore errors
            // when the element has already been removed.
            try
            {
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.hide", SnackbarInstance.Id);
            }
            catch (Exception ex)
            {
                if (ex.Message is not null &&
                    ex.Message.Contains("removeChild") &&
                    ex.Message.Contains("parentNode is null"))
                {
                    LogWarning("JS hide warning: element already removed. {Message}", ex.Message);
                }
                else
                {
                    throw;
                }
            }

            if (OnClose.HasDelegate)
            {
                // Use the Dispatcher to ensure we're on the correct thread for UI updates
                await InvokeAsync(async () =>
                {
                    await OnClose.InvokeAsync();
                });
            }

            LogDebug("Snackbar {Id} closed", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to close DropBearSnackbar {Id}", ex, SnackbarInstance.Id);
        }
    }

    /// <summary>
    ///     JS callback for progress complete or any other relevant event.
    /// </summary>
    [JSInvokable]
    [JSInvokable]
    public async Task OnProgressComplete()
    {
        LogDebug("JS invoked OnProgressComplete for {Id}", SnackbarInstance.Id);
        // Ensure we're on the correct thread when handling JS callback
        await InvokeAsync(async () =>
        {
            await CloseAsync();
        });
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Called by the base class to do final JS cleanup (if needed).
    ///     For example, disposing or removing the snackbar from the JS manager.
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        try
        {
            try
            {
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.dispose", SnackbarInstance.Id);
                LogDebug("Snackbar JS object disposed for {Id}", SnackbarInstance.Id);
            }
            catch (Exception ex)
            {
                if (ex.Message is not null &&
                    ex.Message.Contains("removeChild") &&
                    ex.Message.Contains("parentNode is null"))
                {
                    LogWarning("JS dispose warning: element already removed. {Message}", ex.Message);
                }
                else
                {
                    LogWarning("Error disposing JS resources for {Id}: {Message}", ex, SnackbarInstance.Id, ex.Message);
                }
            }
        }
        catch (JSDisconnectedException)
        {
            LogWarning("Cleanup skipped: JS runtime disconnected.");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Cleanup skipped: Operation cancelled.");
        }
        finally
        {
            _jsModule = null;
            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }
    }
}
