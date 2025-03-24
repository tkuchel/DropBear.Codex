#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A component that renders navigational buttons for going back, going home, and scrolling to top.
///     Optimized for Blazor Server with proper thread synchronization and memory management.
/// </summary>
public sealed partial class DropBearNavigationButtons : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.NavigationButtons;
    private static readonly TimeSpan ScrollThrottleDelay = TimeSpan.FromMilliseconds(100);
    private readonly CancellationTokenSource _navigationCts = new();
    private readonly SemaphoreSlim _navigationSemaphore = new(1, 1);
    private DotNetObjectReference<DropBearNavigationButtons>? _dotNetRef;
    private bool _isInitialized;
    private bool _isNavigating;
    private volatile bool _isVisible;
    private DateTime _lastScrollTime = DateTime.MinValue;
    private IJSObjectReference? _module;

    /// <summary>
    ///     Gets or sets the visibility state of the scroll-to-top button
    /// </summary>
    private bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            _ = QueueStateHasChangedAsync(() => { });
        }
    }

    /// <summary>
    ///     Initializes the component by setting up JS interop
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            // Use non-blocking wait with timeout
            if (!await _navigationSemaphore.WaitAsync(2000, _navigationCts.Token))
            {
                LogWarning("Timeout waiting for navigation semaphore");
                return;
            }

            try
            {
                // Load the JS module and initialize
                var moduleResult = await GetJsModuleAsync(JsModuleName);

                if (moduleResult.IsFailure)
                {
                    LogError("Failed to load JS module: {Error}", moduleResult.Exception);
                    return;
                }

                _module = moduleResult.Value;

                await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", _navigationCts.Token);

                // Create .NET reference for JS callbacks
                _dotNetRef = DotNetObjectReference.Create(this);

                // Initialize the navigation manager with settings
                await _module.InvokeVoidAsync($"{JsModuleName}API.createNavigationManager",
                    _navigationCts.Token,
                    _dotNetRef,
                    new
                    {
                        componentId = ComponentId,
                        scrollThreshold = ScrollThreshold,
                        showScrollTopButton = ShowScrollTopButton
                    });

                _isInitialized = true;
                LogDebug("Navigation buttons initialized");
            }
            finally
            {
                _navigationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("JS initialization interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize navigation buttons", ex);
        }
    }

    /// <summary>
    ///     Navigates back in browser history
    /// </summary>
    private async Task GoBack()
    {
        if (!_isInitialized || IsDisposed || _isNavigating)
        {
            return;
        }

        try
        {
            // Prevent multiple rapid navigations
            _isNavigating = true;

            // Get a non-blocking semaphore lock with timeout
            if (await _navigationSemaphore.WaitAsync(500, _navigationCts.Token))
            {
                try
                {
                    if (_module == null)
                    {
                        var moduleResult = await GetJsModuleAsync(JsModuleName);
                        if (moduleResult.IsFailure)
                        {
                            LogError("Failed to load JS module: {Error}", moduleResult.Exception);
                            return;
                        }

                        _module = moduleResult.Value;
                    }

                    // Navigate back using JS
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_navigationCts.Token);
                    cts.CancelAfter(2000); // 2 second timeout for JS operation

                    await _module.InvokeVoidAsync($"{JsModuleName}API.goBack", cts.Token);
                    LogDebug("Navigated back");
                }
                finally
                {
                    _navigationSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Navigation interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to navigate back", ex);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    ///     Navigates to the home page
    /// </summary>
    private async Task GoHome()
    {
        if (IsDisposed || _isNavigating)
        {
            return;
        }

        try
        {
            // Prevent multiple rapid navigations
            _isNavigating = true;

            await InvokeAsync(() => NavigationManager.NavigateTo(HomePath));
            LogDebug("Navigated home to {HomePath}", HomePath);
        }
        catch (Exception ex)
        {
            LogError("Failed to navigate home", ex);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    ///     Scrolls to the top of the page with throttling
    /// </summary>
    private async Task ScrollToTop()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        // Implement scroll throttling
        var now = DateTime.UtcNow;
        if (now - _lastScrollTime < ScrollThrottleDelay)
        {
            return;
        }

        _lastScrollTime = now;

        try
        {
            // Get a non-blocking semaphore lock with timeout
            if (await _navigationSemaphore.WaitAsync(500, _navigationCts.Token))
            {
                try
                {
                    if (_module == null)
                    {
                        var moduleResult = await GetJsModuleAsync(JsModuleName);
                        if (moduleResult.IsFailure)
                        {
                            LogError("Failed to load JS module: {Error}", moduleResult.Exception);
                            return;
                        }

                        _module = moduleResult.Value;
                    }

                    // Scroll to top using JS
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_navigationCts.Token);
                    cts.CancelAfter(2000); // 2 second timeout for JS operation

                    await _module.InvokeVoidAsync($"{JsModuleName}API.scrollToTop", cts.Token);
                    LogDebug("Scrolled to top");
                }
                finally
                {
                    _navigationSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Scroll interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to scroll to top", ex);
        }
    }

    /// <summary>
    ///     Callback method that can be invoked from JavaScript to update button visibility
    /// </summary>
    [JSInvokable]
    public Task UpdateVisibility(bool isVisible)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        try
        {
            IsVisible = isVisible;
            LogDebug("Visibility updated: {IsVisible}", isVisible);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError("Failed to update visibility", ex);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Cleans up JS resources when component is disposed
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_module != null)
            {
                // Try to dispose JS resources with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_navigationCts.Token);
                cts.CancelAfter(2000); // 2 second timeout

                // Get a non-blocking semaphore lock with timeout
                if (await _navigationSemaphore.WaitAsync(1000, cts.Token))
                {
                    try
                    {
                        await _module.InvokeVoidAsync($"{JsModuleName}API.disposeAPI", cts.Token);
                        LogDebug("Navigation resources cleaned up");
                    }
                    finally
                    {
                        _navigationSemaphore.Release();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup navigation resources", ex);
        }
    }

    /// <summary>
    ///     Disposes component resources
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Cancel any ongoing operations first
            await _navigationCts.CancelAsync();

            // Dispose all resources
            _dotNetRef?.Dispose();
            _navigationSemaphore.Dispose();
            _navigationCts.Dispose();

            // Clear references
            _dotNetRef = null;
            _module = null;
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            LogError("Error during component disposal", ex);
        }

        await base.DisposeAsync();
    }

    #region Parameters

    /// <summary>
    ///     Position of the back button (top)
    /// </summary>
    [Parameter]
    public string BackButtonTop { get; set; } = "20px";

    /// <summary>
    ///     Position of the back button (left)
    /// </summary>
    [Parameter]
    public string BackButtonLeft { get; set; } = "80px";

    /// <summary>
    ///     Position of the home button (top)
    /// </summary>
    [Parameter]
    public string HomeButtonTop { get; set; } = "20px";

    /// <summary>
    ///     Position of the home button (left)
    /// </summary>
    [Parameter]
    public string HomeButtonLeft { get; set; } = "140px";

    /// <summary>
    ///     Position of the scroll-to-top button (bottom)
    /// </summary>
    [Parameter]
    public string ScrollTopButtonBottom { get; set; } = "20px";

    /// <summary>
    ///     Position of the scroll-to-top button (right)
    /// </summary>
    [Parameter]
    public string ScrollTopButtonRight { get; set; } = "20px";

    /// <summary>
    ///     Defines if the back button should be shown
    /// </summary>
    [Parameter]
    public bool ShowBackButton { get; set; } = true;

    /// <summary>
    ///     Defines if the home button should be shown
    /// </summary>
    [Parameter]
    public bool ShowHomeButton { get; set; } = true;

    /// <summary>
    ///     Defines if the scroll-to-top button should be shown when scrolling down
    /// </summary>
    [Parameter]
    public bool ShowScrollTopButton { get; set; } = true;

    /// <summary>
    ///     Home path to navigate to when clicking the home button
    /// </summary>
    [Parameter]
    public string HomePath { get; set; } = "/";

    /// <summary>
    ///     Threshold in pixels for showing the scroll-to-top button
    /// </summary>
    [Parameter]
    public int ScrollThreshold { get; set; } = 300;

    #endregion
}
