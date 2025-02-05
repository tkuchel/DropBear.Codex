using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
/// A component that renders navigational buttons for going back, going home, and scrolling to top.
/// </summary>
public sealed partial class DropBearNavigationButtons : DropBearComponentBase
{
    // -- Private fields --
    private IJSObjectReference? _module;
    private DotNetObjectReference<DropBearNavigationButtons>? _dotNetRef;
    private const string JsModuleName = JsModuleNames.NavigationButtons;

    // Use this property for controlling the 'Scroll to Top' button's visibility
    private bool _isVisible;
    private bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            StateHasChanged();
        }
    }

    #region Parameters

    [Parameter] public string BackButtonTop { get; set; } = "20px";
    [Parameter] public string BackButtonLeft { get; set; } = "80px";
    [Parameter] public string HomeButtonTop { get; set; } = "20px";
    [Parameter] public string HomeButtonLeft { get; set; } = "140px";
    [Parameter] public string ScrollTopButtonBottom { get; set; } = "20px";
    [Parameter] public string ScrollTopButtonRight { get; set; } = "20px";

    #endregion

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
            return;

        try
        {
            // Get (and cache) the module once
            _module = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // Initialize the global navigation-buttons module
            // "DropBearNavigationButtons.initialize()" sets up the JS environment
            await _module.InvokeVoidAsync(
                $"{JsModuleName}API.initialize"
            ).ConfigureAwait(false);

            // Create the NavigationManager instance passing this .NET reference
            _dotNetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                $"{JsModuleName}API.createNavigationManager",
                _dotNetRef
            ).ConfigureAwait(false);

            LogDebug("DropBearNavigationButtons JS interop initialized.");
        }
        catch (Exception ex)
        {
            LogError("Error initializing DropBearNavigationButtons JS interop.", ex);
            throw;
        }
    }

    /// <summary>
    /// Navigate back one step in the browser history.
    /// </summary>
    private async Task GoBack()
    {
        if (IsDisposed) return;

        try
        {
            // Reuse the cached module
            _module ??= await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);
            await _module.InvokeVoidAsync($"{JsModuleName}API.goBack").ConfigureAwait(false);
            LogDebug("Navigated back via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            LogError("Error navigating back", ex);
        }
    }

    /// <summary>
    /// Navigate to the home page ("/") using Blazor NavigationManager.
    /// </summary>
    private void GoHome()
    {
        try
        {
            NavigationManager.NavigateTo("/");
            LogDebug("Navigated to home via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            LogError("Error navigating to home", ex);
        }
    }

    /// <summary>
    /// Scroll the page to the top.
    /// </summary>
    private async Task ScrollToTop()
    {
        if (IsDisposed) return;

        try
        {
            _module ??= await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);
            await _module.InvokeVoidAsync($"{JsModuleName}API.scrollToTop").ConfigureAwait(false);
            LogDebug("Page scrolled to top via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            LogError("Error scrolling to top", ex);
        }
    }

    /// <summary>
    /// JS-invokable method called by the JavaScript code to update the scroll-to-top button's visibility.
    /// </summary>
    /// <param name="isVisible">True if the button should be visible; otherwise false.</param>
    [JSInvokable]
    public void UpdateVisibility(bool isVisible)
    {
        IsVisible = isVisible;
        LogDebug("Scroll-to-top button visibility updated: {IsVisible}", isVisible);
    }

    /// <summary>
    /// Called by the base class during disposal to allow custom JS cleanup
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync($"{JsModuleName}API.disposeAPI").ConfigureAwait(false);
                LogDebug("DropBearNavigationButtons disposed via JS interop.");
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
        catch (Exception ex)
        {
            LogError("Error disposing DropBearNavigationButtons via JS interop.", ex);
        }
        finally
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }
    }
}
