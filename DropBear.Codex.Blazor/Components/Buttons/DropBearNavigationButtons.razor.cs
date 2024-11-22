﻿#region

using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

public sealed partial class DropBearNavigationButtons : ComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearNavigationButtons>();
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isDisposed;
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
                    var isConnected = await JsRuntime.InvokeAsync<bool>("eval", "typeof window !== 'undefined'");
                    if (isConnected)
                    {
                        await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.dispose");
                        Logger.Debug("JS interop for DropBearNavigationButtons disposed.");
                    }
                }
                catch (JSDisconnectedException disconex)
                {
                    Logger.Warning(disconex, "JS interop for DropBearNavigationButtons is already disposed.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during JS interop disposal.");
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

    /// <summary>
    ///     Initializes the JavaScript interop after the component has rendered.
    /// </summary>
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
    }

    /// <summary>
    ///     Navigates to the previous page in the browser history.
    /// </summary>
    private async Task GoBack()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.goBack");
            Logger.Debug("Navigated back.");
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
            Logger.Debug("Navigated to home.");
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
            await JsRuntime.InvokeVoidAsync("DropBearNavigationButtons.scrollToTop");
            Logger.Debug("Page scrolled to top.");
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

    private async Task WaitForJsInitializationAsync(string objectName, int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var isLoaded = await JsRuntime.InvokeAsync<bool>("eval",
                    $"typeof window.{objectName} !== 'undefined' && window.{objectName} !== null");

                if (isLoaded)
                {
                    return;
                }

                await Task.Delay(100); // Wait 100ms before next attempt
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new JSException($"JavaScript object {objectName} failed to initialize after {maxAttempts} attempts");
    }
}
