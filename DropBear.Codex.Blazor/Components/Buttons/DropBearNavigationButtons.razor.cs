#region

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
public sealed partial class DropBearNavigationButtons : ComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearNavigationButtons>();

    // A semaphore to ensure thread-safe initialization/disposal calls.
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isDisposed;
    private bool _isJsInitialized;

    private DotNetObjectReference<DropBearNavigationButtons>? _objRef;

    /// <summary>
    ///     Tracks whether the scroll-to-top button is currently visible.
    ///     Updated by JS interop via <see cref="UpdateVisibility" />.
    /// </summary>
    private bool IsVisible { get; set; }

    /// <summary>
    ///     Disposes the component, stopping any JS interop and releasing resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_isJsInitialized && JsRuntime is not null)
            {
                try
                {
                    // Check if the JS runtime is still connected before disposing.
                    var isConnected = await JsRuntime.InvokeAsync<bool>("eval", "typeof window !== 'undefined'");
                    if (isConnected)
                    {
                        await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.dispose");
                        Logger.Debug("JS interop for DropBearNavigationButtons disposed.");
                    }
                }
                catch (JSDisconnectedException ex)
                {
                    Logger.Warning(ex, "JS interop for DropBearNavigationButtons is already disconnected.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during JS interop disposal for DropBearNavigationButtons.");
                }
            }

            _objRef?.Dispose();
            Logger.Debug("DotNetObjectReference for DropBearNavigationButtons disposed.");
        }
        finally
        {
            _initializationLock.Release();
            _initializationLock.Dispose();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_isDisposed)
        {
            await _initializationLock.WaitAsync();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                await WaitForJsInitializationAsync("DropBearNavigationButtons");
                _objRef = DotNetObjectReference.Create(this);

                await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.initialize", _objRef);
                _isJsInitialized = true;

                Logger.Debug("JS interop for DropBearNavigationButtons initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing JS interop for DropBearNavigationButtons.");
            }
            finally
            {
                _initializationLock.Release();
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
            await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.goBack");
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
            await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.scrollToTop");
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
        StateHasChanged();
    }

    /// <summary>
    ///     Waits until the specified JavaScript object is loaded on the window or times out.
    /// </summary>
    /// <param name="objectName">The name of the JS object to check for.</param>
    /// <param name="maxAttempts">How many times to check before giving up.</param>
    /// <exception cref="JSException">Throws if the JS object can't be found in time.</exception>
    private async Task WaitForJsInitializationAsync(string objectName, int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var isLoaded = await JsRuntime.InvokeAsync<bool>(
                    "eval",
                    $"typeof window.{objectName} !== 'undefined' && window.{objectName} !== null"
                );

                if (isLoaded)
                {
                    return;
                }

                // Wait 100ms before the next attempt
                await Task.Delay(100);
            }
            catch
            {
                // If an exception occurs (maybe JS isn't ready yet), just delay and retry
                await Task.Delay(100);
            }
        }

        throw new JSException(
            $"JavaScript object '{objectName}' failed to initialize after {maxAttempts} attempts."
        );
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
