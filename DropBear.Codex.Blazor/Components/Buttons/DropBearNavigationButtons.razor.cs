#region

using System.Collections.Frozen;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
/// Modern navigation buttons component with enhanced performance for Blazor Server.
/// Leverages .NET 8+ features and implements improved accessibility and UX patterns.
/// </summary>
public sealed partial class DropBearNavigationButtons : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.NavigationButtons;

    // Use frozen collections for better performance in .NET 8+
    private static readonly FrozenDictionary<NavigationButtonPosition, string> PositionClasses =
        new Dictionary<NavigationButtonPosition, string>
        {
            [NavigationButtonPosition.TopLeft] = "nav-buttons--top-left",
            [NavigationButtonPosition.TopRight] = "nav-buttons--top-right",
            [NavigationButtonPosition.BottomLeft] = "nav-buttons--bottom-left",
            [NavigationButtonPosition.BottomRight] = "nav-buttons--bottom-right",
            [NavigationButtonPosition.TopCenter] = "nav-buttons--top-center",
            [NavigationButtonPosition.BottomCenter] = "nav-buttons--bottom-center"
        }.ToFrozenDictionary();

    // Improved constants
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ThrottleDelay = TimeSpan.FromMilliseconds(150);

    // State management
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly CancellationTokenSource _componentCts = new();

    private DotNetObjectReference<DropBearNavigationButtons>? _dotNetRef;
    private bool _isInitialized;
    private DateTime _lastOperationTime = DateTime.MinValue;
    private volatile bool _isVisible;

    /// <summary>
    /// Gets or sets the visibility state with automatic UI updates
    /// </summary>
    private bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    /// Gets the computed container CSS class
    /// </summary>
    private string ContainerCssClass
    {
        get
        {
            var baseClass = "dropbear-nav-buttons";

            if (PositionClasses.TryGetValue(Position, out var positionClass))
            {
                return $"{baseClass} {positionClass}";
            }

            return baseClass;
        }
    }

    /// <summary>
    /// Initialize the component with JavaScript integration
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(OperationTimeout);

            // Get JS module with error handling
            var moduleResult = await GetJsModuleAsync(JsModuleName);
            if (moduleResult.IsSuccess == false)
            {
                LogError("Failed to load navigation buttons JS module", moduleResult.Exception ?? new InvalidOperationException("Module load failed"));
                return;
            }

            var module = moduleResult.Value;

            // Initialize JS module
            await module!.InvokeVoidAsync($"{JsModuleName}API.initialize", cts.Token);

            // Create .NET reference for callbacks
            _dotNetRef = DotNetObjectReference.Create(this);

            // Initialize with configuration
            await module.InvokeVoidAsync($"{JsModuleName}API.createNavigationManager", cts.Token, _dotNetRef);

            _isInitialized = true;
            LogDebug("Navigation buttons initialized successfully");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Navigation buttons initialization interrupted: {ExceptionType}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to initialize navigation buttons");
        }
    }

    /// <summary>
    /// Navigate back with throttling and error handling
    /// </summary>
    private async Task GoBack()
    {
        if (!await CanPerformOperation())
        {
            return;
        }

        try
        {
            await using var _ = await _operationSemaphore.LockAsync(OperationTimeout, ComponentToken);

            var moduleResult = await GetJsModuleAsync(JsModuleName);
            if (moduleResult.IsSuccess == false)
            {
                LogError("Failed to get JS module for back navigation", moduleResult.Exception ?? new InvalidOperationException("Module retrieval failed"));
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(OperationTimeout);

            await moduleResult.Value!.InvokeVoidAsync($"{JsModuleName}API.goBack", cts.Token);

            _lastOperationTime = DateTime.UtcNow;
            LogDebug("Back navigation completed");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Back navigation interrupted: {ExceptionType}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to navigate back");
        }
    }

    /// <summary>
    /// Navigate to home page with improved error handling
    /// </summary>
    private async Task GoHome()
    {
        if (!await CanPerformOperation())
        {
            return;
        }

        try
        {
            await using var _ = await _operationSemaphore.LockAsync(OperationTimeout, ComponentToken);

            await InvokeAsync(() => NavigationManager.NavigateTo(HomePath));

            _lastOperationTime = DateTime.UtcNow;
            LogDebug("Home navigation completed to: {HomePath}", HomePath);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to navigate to home");
        }
    }

    /// <summary>
    /// Scroll to top with enhanced performance and throttling
    /// </summary>
    private async Task ScrollToTop()
    {
        if (!await CanPerformOperation())
        {
            return;
        }

        try
        {
            await using var _ = await _operationSemaphore.LockAsync(OperationTimeout, ComponentToken);

            var moduleResult = await GetJsModuleAsync(JsModuleName);
            if (moduleResult.IsSuccess == false)
            {
                LogError("Failed to get JS module for scroll: {Error}", moduleResult.Exception);
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(OperationTimeout);

            await moduleResult.Value.InvokeVoidAsync($"{JsModuleName}API.scrollToTop", cts.Token);

            _lastOperationTime = DateTime.UtcNow;
            LogDebug("Scroll to top completed");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Scroll operation interrupted: {ExceptionType}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to scroll to top");
        }
    }

    /// <summary>
    /// Check if operation can be performed (throttling and state checks)
    /// </summary>
    private async Task<bool> CanPerformOperation()
    {
        if (!_isInitialized || IsDisposed)
        {
            return false;
        }

        // Throttling check
        var now = DateTime.UtcNow;
        if (now - _lastOperationTime < ThrottleDelay)
        {
            LogDebug("Operation throttled");
            return false;
        }

        // Try to acquire semaphore without blocking
        if (!await _operationSemaphore.WaitAsync(0, ComponentToken))
        {
            LogDebug("Operation blocked - another operation in progress");
            return false;
        }

        _operationSemaphore.Release();
        return true;
    }

    /// <summary>
    /// JavaScript callback for visibility updates
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
            LogDebug("Visibility updated to: {IsVisible}", isVisible);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update visibility");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Clean up JavaScript resources
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_isInitialized)
            {
                var moduleResult = await GetJsModuleAsync(JsModuleName);
                if (moduleResult.IsSuccess)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
                    cts.CancelAfter(OperationTimeout);

                    await moduleResult.Value.InvokeVoidAsync($"{JsModuleName}API.dispose", cts.Token);
                }
            }

            LogDebug("Navigation buttons cleanup completed");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {ExceptionType}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to cleanup navigation buttons");
        }
    }

    /// <summary>
    /// Enhanced disposal following .NET 8 patterns
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _componentCts.CancelAsync();

            _dotNetRef?.Dispose();
            _operationSemaphore.Dispose();
            _componentCts.Dispose();

            _dotNetRef = null;
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during navigation buttons disposal");
        }

        await base.DisposeAsyncCore();
    }

    #region Parameters

    /// <summary>
    /// Position of the navigation button container
    /// </summary>
    [Parameter] public NavigationButtonPosition Position { get; set; } = NavigationButtonPosition.BottomRight;

    /// <summary>
    /// Whether to show the back button
    /// </summary>
    [Parameter] public bool ShowBackButton { get; set; } = true;

    /// <summary>
    /// Whether to show the home button
    /// </summary>
    [Parameter] public bool ShowHomeButton { get; set; } = true;

    /// <summary>
    /// Whether to show the scroll-to-top button
    /// </summary>
    [Parameter] public bool ShowScrollTopButton { get; set; } = true;

    /// <summary>
    /// Home path for navigation
    /// </summary>
    [Parameter] public string HomePath { get; set; } = "/";

    /// <summary>
    /// Scroll threshold in pixels for showing scroll-to-top button
    /// </summary>
    [Parameter] public int ScrollThreshold { get; set; } = 400;

    /// <summary>
    /// Custom offset from edge (e.g., "20px", "1rem")
    /// </summary>
    [Parameter] public string EdgeOffset { get; set; } = "1.5rem";

    /// <summary>
    /// Gap between buttons when multiple are shown
    /// </summary>
    [Parameter] public string ButtonGap { get; set; } = "0.75rem";

    /// <summary>
    /// Animation duration for button transitions
    /// </summary>
    [Parameter] public string AnimationDuration { get; set; } = "200ms";

    /// <summary>
    /// Whether buttons should have a backdrop blur effect
    /// </summary>
    [Parameter] public bool UseBackdropBlur { get; set; } = true;

    /// <summary>
    /// Custom CSS class for the container
    /// </summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>
    /// Event callback when back button is clicked
    /// </summary>
    [Parameter] public EventCallback OnBackClick { get; set; }

    /// <summary>
    /// Event callback when home button is clicked
    /// </summary>
    [Parameter] public EventCallback OnHomeClick { get; set; }

    /// <summary>
    /// Event callback when scroll-to-top button is clicked
    /// </summary>
    [Parameter] public EventCallback OnScrollTopClick { get; set; }

    /// <summary>
    /// Event callback when visibility changes
    /// </summary>
    [Parameter] public EventCallback<bool> OnVisibilityChanged { get; set; }

    #endregion
}
