#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
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
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private static readonly int MAX_RETRIES = 3;
    private static readonly int RETRY_DELAY_MS = 500;
    private readonly AsyncLock _initializationLock = new();
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
        // Call the base implementation
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        // Use an async lock so we don't run initialization concurrently
        using (await _initializationLock.LockAsync(_disposalTokenSource.Token))
        {
            // Check again after acquiring the lock
            if (IsDisposed)
            {
                return;
            }

            try
            {
                // Create DotNetObjectReference only if not already created
                _objRef ??= DotNetObjectReference.Create(this);

                // Perform up to MAX_RETRIES attempts
                var retryCount = 0;
                while (retryCount < MAX_RETRIES)
                {
                    try
                    {
                        // 1) Ensure the JS module is fully registered
                        await EnsureJsModuleInitializedAsync("DropBearNavigationButtons");

                        // 2) Now that the module is loaded, create the navigation manager
                        await SafeJsVoidInteropAsync("DropBearNavigationButtons.createNavigationManager", _objRef);

                        Logger.Debug("JS interop for DropBearNavigationButtons initialized.");
                        break; // success
                    }
                    catch (Exception ex) when (retryCount < MAX_RETRIES - 1)
                    {
                        Logger.Warning(ex, "Retry {Count} initializing DropBearNavigationButtons", retryCount + 1);
                        await Task.Delay(RETRY_DELAY_MS);
                        retryCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing JS interop for DropBearNavigationButtons.");
                throw;
            }
        }
    }


    /// <summary>
    ///     Navigates back one step in the browser history via JS interop.
    /// </summary>
    private async Task GoBack()
    {
        try
        {
            await EnsureJsModuleInitializedAsync("DropBearNavigationButtons");
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
            await EnsureJsModuleInitializedAsync("DropBearNavigationButtons");
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
            await EnsureJsModuleInitializedAsync("DropBearNavigationButtons");
            await SafeJsVoidInteropAsync("DropBearNavigationButtons.dispose");
            Logger.Debug("JS interop for DropBearNavigationButtons disposed.");
        }
        catch (JSDisconnectedException jsDisconnectedException)
        {
            Logger.Warning(jsDisconnectedException,
                "Error disposing JS interop for DropBearNavigationButtons, JS Disconnected.");
        }
        catch (ObjectDisposedException objectDisposedException)
        {
            Logger.Warning(objectDisposedException,
                "Error disposing JS interop for DropBearNavigationButtons, already disposed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during JS interop disposal for DropBearNavigationButtons.");
        }
        finally
        {
            _objRef?.Dispose();
            _objRef = null;
            Logger.Debug("DotNetObjectReference for DropBearNavigationButtons disposed.");
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            await CleanupJavaScriptResourcesAsync();
        }

        await base.DisposeAsync(disposing);
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
