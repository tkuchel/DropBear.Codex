#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A component that renders navigational buttons for going back, going home, and scrolling to top.
///     Manages JavaScript interop for dynamic visibility and disposal.
/// </summary>
public sealed partial class DropBearNavigationButtons : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearNavigationButtons>();
    private bool _isVisible;
    private DotNetObjectReference<DropBearNavigationButtons>? _objRef;

    /// <summary>
    ///     Tracks whether the scroll-to-top button is currently visible.
    ///     Updated by JS interop via <see cref="UpdateVisibility" />.
    /// </summary>
    private bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                StateHasChanged();
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !IsDisposed)
        {
            try
            {
                _objRef = DotNetObjectReference.Create(this);
                await SafeJsVoidInteropAsync("DropBearNavigationButtons.createNavigationManager", _objRef);
                Logger.Debug("JS interop for DropBearNavigationButtons initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing JS interop for DropBearNavigationButtons.");
                throw;
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    /// <summary>
    ///     Navigates back one step in the browser history via JS interop.
    /// </summary>
    private async Task GoBack()
    {
        try
        {
            await SafeJsVoidInteropAsync("DropBearNavigationButtons.goBack");
            Logger.Debug("Navigated back via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error navigating back.");
        }
    }

    /// <summary>
    ///     Navigates to the home page ('/') using the Blazor NavigationManager.
    /// </summary>
    private void GoHome()
    {
        try
        {
            NavigationManager.NavigateTo("/");
            Logger.Debug("Navigated to home via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error navigating to home.");
        }
    }

    /// <summary>
    ///     Scrolls the page to the top via JS interop.
    /// </summary>
    private async Task ScrollToTop()
    {
        try
        {
            await SafeJsVoidInteropAsync("DropBearNavigationButtons.scrollToTop");
            Logger.Debug("Page scrolled to top via DropBearNavigationButtons.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error scrolling to top.");
        }
    }

    /// <summary>
    ///     JS-invokable method called by the JavaScript code to update the button's visibility.
    /// </summary>
    /// <param name="isVisible">True if the button should be visible; otherwise, false.</param>
    [JSInvokable]
    public void UpdateVisibility(bool isVisible)
    {
        IsVisible = isVisible;
        Logger.Debug("Scroll-to-top button visibility updated to: {IsVisible}", isVisible);
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await SafeJsVoidInteropAsync("DropBearNavigationButtons.dispose");
            Logger.Debug("JS interop for DropBearNavigationButtons disposed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during JS interop disposal for DropBearNavigationButtons.");
        }
        finally
        {
            _objRef?.Dispose();
            Logger.Debug("DotNetObjectReference for DropBearNavigationButtons disposed.");
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
}
