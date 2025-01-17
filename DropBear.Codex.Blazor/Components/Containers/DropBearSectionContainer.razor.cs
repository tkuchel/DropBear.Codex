#region

using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can optionally center its content horizontally/vertically.
///     Implements enhanced resource management, performance optimizations, and lifecycle handling.
/// </summary>
public sealed partial class DropBearSectionContainer : DropBearComponentBase
{
    // Default styles
    private const string DefaultMaxWidth = "100%";
    private const int MaxJsInitializationAttempts = 50;
    private const int JsInitializationDelayMs = 100;
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSectionContainer>();
    private readonly CancellationTokenSource _disposalTokenSource = new();

    // Thread-safe initialization control
    private readonly AsyncLock _initializationLock = new();
    private WindowDimensions? _cachedDimensions;
    private string? _containerClass;

    // Component state management
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private volatile bool _isDisposed;
    private volatile bool _isJsInitialized;
    private string? _maxWidth;

    /// <summary>
    ///     Gets or sets the maximum width style with validation.
    /// </summary>
    private string MaxWidthStyle { get; set; } = DefaultMaxWidth;

    /// <summary>
    ///     The content rendered within the container.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     The maximum width of the container, e.g., "800px" or "70%".
    /// </summary>
    [Parameter]
    public string? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (value != null && !value.EndsWith("%") && !value.EndsWith("px"))
            {
                throw new ArgumentException("MaxWidth must end with % or px", nameof(MaxWidth));
            }

            _maxWidth = value;
        }
    }

    /// <summary>
    ///     If true, horizontally center the container inside its parent.
    /// </summary>
    [Parameter]
    public bool IsHorizontalCentered { get; set; }

    /// <summary>
    ///     If true, vertically center the container inside its parent.
    /// </summary>
    [Parameter]
    public bool IsVerticalCentered { get; set; }

    /// <summary>
    ///     Event callback for when the container dimensions change.
    /// </summary>
    [Parameter]
    public EventCallback<WindowDimensions> OnDimensionsChanged { get; set; }

    /// <summary>
    ///     Dynamically builds and caches the CSS class for the container.
    /// </summary>
    private string ContainerClass => _containerClass ??= BuildContainerClass();

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        using (await _initializationLock.LockAsync())
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _isDisposed = true;
                await _disposalTokenSource.CancelAsync();

                if (_isJsInitialized)
                {
                    await CleanupJsResourcesAsync();
                }

                _dotNetRef?.Dispose();
                _disposalTokenSource.Dispose();
                _initializationLock.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during component disposal");
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _isDisposed)
        {
            return;
        }

        using (await _initializationLock.LockAsync(_disposalTokenSource.Token))
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                await InitializeJsInteropAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during initialization");
                // Consider surfacing this error to the user interface
            }
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _containerClass = null; // Invalidate cache
        base.OnParametersSet();
    }

    /// <summary>
    ///     JS-invokable method that recalculates and sets the container's max width.
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var dimensions = await GetWindowDimensionsAsync();
            if (dimensions is null)
            {
                return;
            }

            var newMaxWidth = CalculateMaxWidth(dimensions.Width);
            if (MaxWidthStyle != newMaxWidth)
            {
                MaxWidthStyle = newMaxWidth;
                await InvokeAsync(StateHasChanged);
                await NotifyDimensionsChangedAsync(dimensions);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while setting max width");
            MaxWidthStyle = DefaultMaxWidth;
        }
    }

    /// <summary>
    ///     Initializes JavaScript interop functionality.
    /// </summary>
    private async Task InitializeJsInteropAsync()
    {
        try
        {
            await WaitForJsInitializationAsync("DropBearResizeManager");
            _dotNetRef = DotNetObjectReference.Create(this);
            await JsRuntime.InvokeVoidAsync("DropBearResizeManager.initialize", _dotNetRef);
            _isJsInitialized = true;
            Logger.Debug("Resize event listener registered");

            await SetMaxWidthBasedOnWindowSize();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS initialization failed");
            throw;
        }
    }

    /// <summary>
    ///     Cleans up JavaScript resources safely.
    /// </summary>
    private async Task CleanupJsResourcesAsync()
    {
        try
        {
            var isConnected = await JsRuntime.InvokeAsync<bool>(
                "eval",
                "typeof window !== 'undefined'"
            );

            if (isConnected)
            {
                await JsRuntime.InvokeVoidAsync("DropBearResizeManager.dispose");
                Logger.Debug("Resize event listener disposed");
            }
        }
        catch (JSDisconnectedException ex)
        {
            Logger.Warning(ex, "JS resources already disposed (disconnected)");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during JS cleanup");
        }
    }

    /// <summary>
    ///     Builds the container CSS class efficiently using StringBuilder.
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
    ///     Calculates the max width based on window dimensions and MaxWidth parameter.
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
                return $"{calculatedWidth}px";
            }

            Logger.Error("Failed to parse MaxWidth percentage: {MaxWidth}", MaxWidth);
            return DefaultMaxWidth;
        }

        return MaxWidth; // Direct use for pixel values
    }

    /// <summary>
    ///     Gets the window dimensions with caching for performance.
    /// </summary>
    private async Task<WindowDimensions?> GetWindowDimensionsAsync()
    {
        try
        {
            _cachedDimensions = await JsRuntime.InvokeAsync<WindowDimensions>(
                "getWindowDimensions",
                _disposalTokenSource.Token
            );
            return _cachedDimensions;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get window dimensions");
            return null;
        }
    }

    /// <summary>
    ///     Notifies listeners of dimension changes.
    /// </summary>
    private async Task NotifyDimensionsChangedAsync(WindowDimensions dimensions)
    {
        if (OnDimensionsChanged.HasDelegate)
        {
            try
            {
                await OnDimensionsChanged.InvokeAsync(dimensions);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error notifying dimension change");
            }
        }
    }

    /// <summary>
    ///     Waits for JavaScript initialization with timeout and cancellation support.
    /// </summary>
    private async Task WaitForJsInitializationAsync(string objectName)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token,
            _disposalTokenSource.Token
        );

        var attempts = 0;
        while (attempts++ < MaxJsInitializationAttempts)
        {
            try
            {
                var isLoaded = await JsRuntime.InvokeAsync<bool>(
                    "eval",
                    linkedCts.Token,
                    $"typeof window.{objectName} !== 'undefined' && window.{objectName} !== null"
                );

                if (isLoaded)
                {
                    return;
                }

                await Task.Delay(JsInitializationDelayMs, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"JavaScript object {objectName} initialization timed out");
            }
            catch when (_disposalTokenSource.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Component disposal requested during initialization");
            }
            catch
            {
                if (attempts < MaxJsInitializationAttempts)
                {
                    await Task.Delay(JsInitializationDelayMs, linkedCts.Token);
                }
                else
                {
                    throw;
                }
            }
        }

        throw new JSException(
            $"JavaScript object {objectName} failed to initialize after {MaxJsInitializationAttempts} attempts");
    }
}
