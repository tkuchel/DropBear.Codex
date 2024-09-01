#region

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

public sealed partial class DropBearNavigationButtons : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<DropBearNavigationButtons> _objRef;
    private bool isJsInitialized;
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
        if (isJsInitialized)
        {
            await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.dispose");
        }

        _objRef?.Dispose();
    }

    /// <summary>
    ///     Initializes the JavaScript interop after the component has rendered.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _objRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("DropBearNavigationButtons.initialize", _objRef);
            isJsInitialized = true;
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
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error navigating back: {ex.Message}");
            // Consider logging this error or showing a user-friendly message
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
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error navigating home: {ex.Message}");
            // Consider logging this error or showing a user-friendly message
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
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scrolling to top: {ex.Message}");
            // Consider logging this error or showing a user-friendly message
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
        StateHasChanged();
    }
}
