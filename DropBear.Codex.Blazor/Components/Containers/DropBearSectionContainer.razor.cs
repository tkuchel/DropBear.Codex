using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can optionally center its content horizontally/vertically.
/// </summary>
public sealed partial class DropBearSectionContainer : DropBearComponentBase
{
    // -- Constants & Defaults --
    private const string DEFAULT_MAX_WIDTH = "100%";

    // -- Private fields for JS interop and state --
    private IJSObjectReference? _module;
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private const string JsModuleName = JsModuleNames.ResizeManager;

    private WindowDimensions? _cachedDimensions;
    private string? _containerClassCache;
    private string _maxWidthStyle = DEFAULT_MAX_WIDTH;

    // The backing field for MaxWidth
    private string? _maxWidth;

    #region Parameters

    /// <summary>
    ///     The content rendered within the container.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     The maximum width of the container, e.g. "800px" or "70%".
    /// </summary>
    [Parameter]
    public string? MaxWidth
    {
        get => _maxWidth;
        set
        {
            // Validate that MaxWidth ends with '%' or 'px'
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

    #endregion

    /// <summary>
    ///     Gets a CSS class for the container, caching the result until parameters change.
    /// </summary>
    private string ContainerClass => _containerClassCache ??= BuildContainerClass();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Clear cached container class so it recomputes if any parameter changed
        _containerClassCache = null;
        base.OnParametersSet();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(false);

        if (!firstRender || IsDisposed)
            return;

        try
        {
            // Load (and cache) our "resize-manager" JS module
            _module = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // 1) Initialize the module
            await _module.InvokeVoidAsync(
                $"{JsModuleName}API.initialize"
            ).ConfigureAwait(false);

            // 2) Create a new resize manager instance in JS, passing a .NET reference for callback
            _dotNetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                $"{JsModuleName}API.createResizeManager",
                _dotNetRef
            ).ConfigureAwait(false);

            LogDebug("DropBearSectionContainer JS interop initialized.");

            // Optionally set the width immediately
            await SetMaxWidthBasedOnWindowSize();
        }
        catch (Exception ex)
        {
            LogError("Error during DropBearSectionContainer initialization", ex);
            // Optionally handle or surface the error to UI
        }
    }

    /// <summary>
    ///     JS-invokable method that recalculates and sets the container's max width.
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        if (IsDisposed)
            return;

        try
        {
            var dimensions = await GetWindowDimensionsAsync().ConfigureAwait(false);
            if (dimensions is null)
                return;

            var newMaxWidth = CalculateMaxWidth(dimensions.Width);
            if (_maxWidthStyle != newMaxWidth)
            {
                _maxWidthStyle = newMaxWidth;

                // Update the UI and optionally notify that dimensions changed
                await InvokeStateHasChangedAsync(async () =>
                {
                    await NotifyDimensionsChangedAsync(dimensions).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError("Error setting max width", ex);
            _maxWidthStyle = DEFAULT_MAX_WIDTH;
        }
    }

    /// <summary>
    ///     Requests window dimensions from the JS side.
    /// </summary>
    private async Task<WindowDimensions?> GetWindowDimensionsAsync()
    {
        if (IsDisposed)
            return _cachedDimensions;

        try
        {
            // Reuse the module reference (and re-fetch it if needed)
            _module ??= await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // "DropBearResizeManager.getDimensions" is a function on the window scope
            var dimensions = await _module.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getDimensions",
                ComponentToken
            ).ConfigureAwait(false);

            _cachedDimensions = dimensions;
            return dimensions;
        }
        catch (Exception ex)
        {
            LogError("Failed to get window dimensions", ex);
            return _cachedDimensions;
        }
    }

    /// <summary>
    ///     Builds the container CSS class based on centering options.
    /// </summary>
    private string BuildContainerClass()
    {
        var builder = new StringBuilder("section-container", 100);

        if (IsHorizontalCentered)
            builder.Append(" horizontal-centered");

        if (IsVerticalCentered)
            builder.Append(" vertical-centered");

        return builder.ToString();
    }

    /// <summary>
    ///     Calculates the max-width style based on the specified (percent or px) MaxWidth
    ///     and the current window width.
    /// </summary>
    private string CalculateMaxWidth(double windowWidth)
    {
        // If no MaxWidth param is specified, default to 100%
        if (string.IsNullOrEmpty(MaxWidth))
            return DEFAULT_MAX_WIDTH;

        // If MaxWidth is "70%", parse the numeric portion and convert to px
        if (MaxWidth.EndsWith("%"))
        {
            if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
            {
                var calculatedWidth = windowWidth * (percentage / 100);
                return $"{calculatedWidth:F0}px";
            }

            LogWarning("Failed to parse MaxWidth percentage: {0}", MaxWidth);
            return DEFAULT_MAX_WIDTH;
        }

        // Otherwise, assume it's a pixel value like "800px"
        return MaxWidth;
    }

    /// <summary>
    ///     Notifies any registered listeners that the container's dimensions changed.
    /// </summary>
    private async Task NotifyDimensionsChangedAsync(WindowDimensions dimensions)
    {
        if (!OnDimensionsChanged.HasDelegate)
            return;

        try
        {
            await OnDimensionsChanged.InvokeAsync(dimensions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Error notifying dimension change", ex);
        }
    }

    /// <summary>
    ///     Called during disposal to clean up JS resources.
    ///     The base class automatically calls this in DisposeCoreAsync().
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync($"{JsModuleName}API.dispose").ConfigureAwait(false);
                LogDebug("Resize manager disposed in JS.");
            }
        }
        catch (JSDisconnectedException jsEx)
        {
            LogWarning("JS disconnected while disposing DropBearSectionContainer. {Message}", jsEx.Message);
        }
        catch (ObjectDisposedException objEx)
        {
            LogWarning("JS object already disposed for DropBearSectionContainer. {Message}", objEx.Message);
        }
        catch (Exception ex)
        {
            LogError("Error cleaning up JS resources for DropBearSectionContainer", ex);
        }
        finally
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }
    }
}
