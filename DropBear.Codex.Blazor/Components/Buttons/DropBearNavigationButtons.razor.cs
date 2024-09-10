#region

using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

public sealed partial class DropBearNavigationButtons : ComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearNavigationButtons>();
    private bool _isJsInitialized;
    private DotNetObjectReference<DropBearNavigationButtons>? _objRef;
    private bool IsVisible { get; set; }

    [Parameter] public string BackButtonTop { get; set; } = "20px";
    [Parameter] public string BackButtonLeft { get; set; } = "80px";
    [Parameter] public string HomeButtonTop { get; set; } = "20px";
    [Parameter] public string HomeButtonLeft { get; set; } = "140px";
    [Parameter] public string ScrollTopButtonBottom { get; set; } = "20px";
    [Parameter] public string ScrollTopButtonRight { get; set; } = "20px";

    /// <summary>
    ///     Cleans up resources when the component is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isJsInitialized && JSRuntime is not null)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.dispose");
                Logger.Debug("JS interop for DropBearNavigationButtons disposed.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during JS interop disposal.");
            }
        }

        _objRef?.Dispose();
        Logger.Debug("DotNetObjectReference for DropBearNavigationButtons disposed.");
    }

    /// <summary>
    ///     Initializes the JavaScript interop after the component has rendered.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _objRef = DotNetObjectReference.Create(this);

            try
            {
                await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.initialize", _objRef);
                _isJsInitialized = true;
                Logger.Debug("JS interop for DropBearNavigationButtons initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing JS interop for DropBearNavigationButtons.");
            }
        }
    }

    /// <summary>
    ///     Navigates to the previous page in the browser history.
    /// </summary>
    private async Task GoBack()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.goBack");
            Logger.Information("Navigated back.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error navigating back.");
        }
    }

    /// <summary>
    ///     Navigates to the home page of the application.
    /// </summary>
    private void GoHome()
    {
        try
        {
            NavigationManager.NavigateTo("/");
            Logger.Information("Navigated to home.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error navigating to home.");
        }
    }

    /// <summary>
    ///     Scrolls the page to the top.
    /// </summary>
    private async Task ScrollToTop()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.scrollToTop");
            Logger.Information("Page scrolled to top.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error scrolling to top.");
        }
    }

    /// <summary>
    ///     Updates the visibility of the scroll-to-top button.
    ///     This method is called from JavaScript.
    /// </summary>
    /// <param name="isVisible">Whether the button should be visible.</param>
    [JSInvokable]
    public void UpdateVisibility(bool isVisible)
    {
        IsVisible = isVisible;
        Logger.Debug("Scroll-to-top button visibility updated: {IsVisible}", isVisible);
        StateHasChanged();
    }
}
