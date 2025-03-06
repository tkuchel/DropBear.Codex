#region

using System.Collections.Concurrent;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can optionally center its content.
///     Optimized for Blazor Server with proper thread safety and state management.
/// </summary>
public partial class DropBearSectionContainer : DropBearComponentBase
{
    private const string DefaultMaxWidth = "100%";
    private const string JsModuleName = JsModuleNames.ResizeManager;
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(3); // Increased cache duration
    private static readonly ConcurrentDictionary<string, WindowDimensions> GlobalDimensionsCache = new();
    private static readonly TimeSpan ResizeDebounceDelay = TimeSpan.FromMilliseconds(150);
    private readonly CancellationTokenSource _containerCts = new();

    private readonly SemaphoreSlim _resizeSemaphore = new(1, 1);

    // Component state
    private WindowDimensions? _cachedDimensions;

    // Performance tracking
    private string _cacheKey = string.Empty;
    private string? _containerClassCache;
    private int _debouncePending;
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private volatile bool _isInitialized;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private string? _maxWidth;
    private string _maxWidthStyle = DefaultMaxWidth;
    private IJSObjectReference? _module;
    private bool _parametersChanged;

    // Debounce state
    private CancellationTokenSource? _resizeDebouncer;

    /// <summary>
    ///     Gets the container CSS class with caching
    /// </summary>
    protected string ContainerClass => _containerClassCache ??= BuildContainerClass();

    /// <summary>
    ///     Child content to be rendered inside the container
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Maximum width of the container (e.g. "800px" or "80%")
    /// </summary>
    [Parameter]
    public string? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (value == _maxWidth)
            {
                return;
            }

            if (value != null && !value.EndsWith("%") && !value.EndsWith("px"))
            {
                throw new ArgumentException("MaxWidth must end with % or px", nameof(MaxWidth));
            }

            _maxWidth = value;
            _parametersChanged = true;
        }
    }

    /// <summary>
    ///     Whether to center the container horizontally
    /// </summary>
    [Parameter]
    public bool IsHorizontalCentered { get; set; }

    /// <summary>
    ///     Whether to center the container vertically
    /// </summary>
    [Parameter]
    public bool IsVerticalCentered { get; set; }

    /// <summary>
    ///     Event callback when window dimensions change
    /// </summary>
    [Parameter]
    public EventCallback<WindowDimensions> OnDimensionsChanged { get; set; }

    /// <summary>
    ///     Updates the parameter tracking when parameters are set
    /// </summary>
    protected override void OnParametersSet()
    {
        // Only rebuild class if alignment parameters changed
        var horizontalChanged = _containerClassCache != null &&
                                IsHorizontalCentered != _containerClassCache.Contains("horizontal-centered");
        var verticalChanged = _containerClassCache != null &&
                              IsVerticalCentered != _containerClassCache.Contains("vertical-centered");

        if (horizontalChanged || verticalChanged || _containerClassCache == null)
        {
            _containerClassCache = null;
            _parametersChanged = true;
        }

        base.OnParametersSet();
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
            // Try to get the semaphore with a timeout to avoid deadlocks
            if (!await _resizeSemaphore.WaitAsync(2000, _containerCts.Token))
            {
                LogWarning("Timeout waiting for resize semaphore during initialization");
                return;
            }

            try
            {
                // Generate a unique cache key for the window dimensions
                _cacheKey = $"dims_{JsRuntime.GetHashCode()}";

                // Initialize JS module
                _module = await GetJsModuleAsync(JsModuleName);
                await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", _containerCts.Token);

                // Create .NET reference
                _dotNetRef = DotNetObjectReference.Create(this);

                // Initialize resizing with configurable parameters
                await _module.InvokeVoidAsync($"{JsModuleName}API.createResizeManager",
                    _containerCts.Token, _dotNetRef,
                    new { debounceMs = (int)ResizeDebounceDelay.TotalMilliseconds, componentId = ComponentId });

                _isInitialized = true;
                LogDebug("Section container initialized");

                // Initial width calculation
                await SetMaxWidthBasedOnWindowSize();
            }
            finally
            {
                _resizeSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("JS initialization interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize section container", ex);
        }
    }

    /// <summary>
    ///     JS-invokable method to update container width on window resize
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            // Track pending debounce calls with thread-safe increment
            Interlocked.Increment(ref _debouncePending);

            // Debounce resize events
            if (_resizeDebouncer != null)
            {
                await _resizeDebouncer.CancelAsync();
            }

            _resizeDebouncer = new CancellationTokenSource();
            var token = _resizeDebouncer.Token;

            // Additional debounce in case JS debounce isn't working as expected
            await Task.Delay((int)ResizeDebounceDelay.TotalMilliseconds, token);

            // Only process if we can acquire the semaphore without blocking for too long
            if (!await _resizeSemaphore.WaitAsync(500, token))
            {
                LogDebug("Skipping resize update - semaphore busy");
                return;
            }

            try
            {
                // Get dimensions (using cached values when possible)
                var dimensions = await GetWindowDimensionsAsync(token);
                if (dimensions == null)
                {
                    return;
                }

                // Calculate new width
                var newMaxWidth = CalculateMaxWidth(dimensions.Width);

                // Only trigger UI update if width changed
                if (_maxWidthStyle != newMaxWidth)
                {
                    await QueueStateHasChangedAsync(async () =>
                    {
                        _maxWidthStyle = newMaxWidth;
                        if (OnDimensionsChanged.HasDelegate)
                        {
                            await OnDimensionsChanged.InvokeAsync(dimensions);
                        }
                    });
                }
            }
            finally
            {
                _resizeSemaphore.Release();
                Interlocked.Decrement(ref _debouncePending);
            }
        }
        catch (OperationCanceledException)
        {
            // Debouncing in action, ignore
            Interlocked.Decrement(ref _debouncePending);
        }
        catch (Exception ex)
        {
            LogError("Error updating max width", ex);
            _maxWidthStyle = DefaultMaxWidth;
            Interlocked.Decrement(ref _debouncePending);
        }
        finally
        {
            if (_resizeDebouncer != null)
            {
                _resizeDebouncer.Dispose();
                _resizeDebouncer = null;
            }
        }
    }

    /// <summary>
    ///     Gets window dimensions with efficient caching
    /// </summary>
    private async Task<WindowDimensions?> GetWindowDimensionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || IsDisposed)
        {
            return _cachedDimensions;
        }

        // Check internal component cache first
        if (_cachedDimensions != null &&
            DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            return _cachedDimensions;
        }

        // Then check global shared cache
        if (!string.IsNullOrEmpty(_cacheKey) &&
            GlobalDimensionsCache.TryGetValue(_cacheKey, out var cachedDimensions) &&
            DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            _cachedDimensions = cachedDimensions;
            _lastDimensionsCheck = DateTime.UtcNow;
            return _cachedDimensions;
        }

        try
        {
            // Ensure module is initialized
            _module ??= await GetJsModuleAsync(JsModuleName);

            // Get dimensions from JS with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(2000); // 2 second timeout for JS operation

            _cachedDimensions = await _module.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getDimensions",
                cts.Token);

            // Update caches
            _lastDimensionsCheck = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(_cacheKey))
            {
                GlobalDimensionsCache[_cacheKey] = _cachedDimensions;
            }

            return _cachedDimensions;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("JS operation interrupted: {Reason}", ex.GetType().Name);
            return _cachedDimensions;
        }
        catch (Exception ex)
        {
            LogError("Failed to get window dimensions", ex);
            return _cachedDimensions;
        }
    }

    /// <summary>
    ///     Builds the container CSS class efficiently
    /// </summary>
    private string BuildContainerClass()
    {
        var builder = new StringBuilder("section-container", 100);

        if (IsHorizontalCentered)
        {
            builder.Append(" horizontal-centered");
        }

        if (IsVerticalCentered)
        {
            builder.Append(" vertical-centered");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Calculates the max width style based on window width
    /// </summary>
    private string CalculateMaxWidth(double windowWidth)
    {
        if (string.IsNullOrEmpty(MaxWidth))
        {
            return DefaultMaxWidth;
        }

        if (MaxWidth.EndsWith("%"))
        {
            if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
            {
                var calculatedWidth = windowWidth * (percentage / 100);
                return $"{calculatedWidth:F0}px";
            }

            LogWarning("Invalid MaxWidth percentage: {MaxWidth}", MaxWidth);
            return DefaultMaxWidth;
        }

        return MaxWidth;
    }

    /// <summary>
    ///     Cleans up JS resources
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // Cancel any pending operations
            if (_resizeDebouncer != null)
            {
                await _resizeDebouncer.CancelAsync();
                _resizeDebouncer.Dispose();
                _resizeDebouncer = null;
            }

            // Dispose JS resources with timeout
            if (_module != null)
            {
                // Try to acquire the semaphore with a short timeout
                if (await _resizeSemaphore.WaitAsync(500, _containerCts.Token))
                {
                    try
                    {
                        // Create a timeout to prevent blocking disposal
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_containerCts.Token);
                        cts.CancelAfter(2000);

                        await _module.InvokeVoidAsync($"{JsModuleName}API.dispose", cts.Token);
                        LogDebug("Section container resources cleaned up");
                    }
                    finally
                    {
                        _resizeSemaphore.Release();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup section container", ex);
        }
    }

    /// <summary>
    ///     Disposes component resources
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Cancel operations first
            await _containerCts.CancelAsync();

            // Clean up resources
            _dotNetRef?.Dispose();
            _resizeSemaphore.Dispose();
            _containerCts.Dispose();

            if (_resizeDebouncer != null)
            {
                _resizeDebouncer.Dispose();
            }

            // Clear references
            _dotNetRef = null;
            _module = null;
            _isInitialized = false;
            _cachedDimensions = null;
            _resizeDebouncer = null;

            // Remove from global cache
            if (!string.IsNullOrEmpty(_cacheKey))
            {
                GlobalDimensionsCache.TryRemove(_cacheKey, out _);
            }
        }
        catch (Exception ex)
        {
            LogError("Error during component disposal", ex);
        }

        await base.DisposeAsync();
    }
}
