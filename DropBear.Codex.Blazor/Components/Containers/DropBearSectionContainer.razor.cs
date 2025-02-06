#region

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
public sealed partial class DropBearSectionContainer : DropBearComponentBase
{
    private const string DEFAULT_MAX_WIDTH = "100%";
    private const string JsModuleName = JsModuleNames.ResizeManager;
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _resizeSemaphore = new(1, 1);
    private WindowDimensions? _cachedDimensions;
    private string? _containerClassCache;
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private volatile bool _isInitialized;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private string? _maxWidth;
    private string _maxWidthStyle = DEFAULT_MAX_WIDTH;

    private IJSObjectReference? _module;
    private CancellationTokenSource? _resizeDebouncer;

    protected string ContainerClass => _containerClassCache ??= BuildContainerClass();

    [Parameter] [EditorRequired] public RenderFragment? ChildContent { get; set; }

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

    [Parameter] public bool IsHorizontalCentered { get; set; }

    [Parameter] public bool IsVerticalCentered { get; set; }

    [Parameter] public EventCallback<WindowDimensions> OnDimensionsChanged { get; set; }

    protected override void OnParametersSet()
    {
        _containerClassCache = null;
        base.OnParametersSet();
    }

    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _resizeSemaphore.WaitAsync(ComponentToken);

            _module = await GetJsModuleAsync(JsModuleName);
            await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", ComponentToken);

            _dotNetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync($"{JsModuleName}API.createResizeManager",
                ComponentToken, _dotNetRef);

            _isInitialized = true;
            LogDebug("Section container initialized");

            await SetMaxWidthBasedOnWindowSize();
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize section container", ex);
            throw;
        }
        finally
        {
            _resizeSemaphore.Release();
        }
    }

    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            // Debounce resize events
            _resizeDebouncer?.Cancel();
            _resizeDebouncer = new CancellationTokenSource();
            var token = _resizeDebouncer.Token;

            await Task.Delay(50, token);

            await _resizeSemaphore.WaitAsync(token);
            try
            {
                var dimensions = await GetWindowDimensionsAsync(token);
                if (dimensions == null)
                {
                    return;
                }

                var newMaxWidth = CalculateMaxWidth(dimensions.Width);
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
            }
        }
        catch (OperationCanceledException)
        {
            // Debouncing in action, ignore
        }
        catch (Exception ex)
        {
            LogError("Error updating max width", ex);
            _maxWidthStyle = DEFAULT_MAX_WIDTH;
        }
        finally
        {
            _resizeDebouncer?.Dispose();
            _resizeDebouncer = null;
        }
    }

    private async Task<WindowDimensions?> GetWindowDimensionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || IsDisposed)
        {
            return _cachedDimensions;
        }

        // Use cached dimensions if recent enough
        if (_cachedDimensions != null &&
            DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            return _cachedDimensions;
        }

        try
        {
            if (_module == null)
            {
                _module = await GetJsModuleAsync(JsModuleName);
            }

            _cachedDimensions = await _module.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getDimensions",
                cancellationToken);

            _lastDimensionsCheck = DateTime.UtcNow;
            return _cachedDimensions;
        }
        catch (Exception ex)
        {
            LogError("Failed to get window dimensions", ex);
            return _cachedDimensions;
        }
    }

    private string BuildContainerClass()
    {
        return new StringBuilder("section-container", 100)
            .Append(IsHorizontalCentered ? " horizontal-centered" : string.Empty)
            .Append(IsVerticalCentered ? " vertical-centered" : string.Empty)
            .ToString();
    }

    private string CalculateMaxWidth(double windowWidth)
    {
        if (string.IsNullOrEmpty(MaxWidth))
        {
            return DEFAULT_MAX_WIDTH;
        }

        if (MaxWidth.EndsWith("%"))
        {
            if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
            {
                var calculatedWidth = windowWidth * (percentage / 100);
                return $"{calculatedWidth:F0}px";
            }

            LogWarning("Invalid MaxWidth percentage: {MaxWidth}", MaxWidth);
            return DEFAULT_MAX_WIDTH;
        }

        return MaxWidth;
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            _resizeDebouncer?.Cancel();

            if (_module != null)
            {
                await _resizeSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await _module.InvokeVoidAsync($"{JsModuleName}API.dispose",
                        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
                    LogDebug("Section container resources cleaned up");
                }
                finally
                {
                    _resizeSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup section container", ex);
        }
        finally
        {
            try
            {
                _dotNetRef?.Dispose();
                _resizeSemaphore.Dispose();
                _resizeDebouncer?.Dispose();
            }
            catch (ObjectDisposedException) { }

            _dotNetRef = null;
            _module = null;
            _isInitialized = false;
            _cachedDimensions = null;
        }
    }
}
