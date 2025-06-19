#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Badges;

/// <summary>
///     A Blazor component for displaying badges with optional tooltips.
///     Optimized for Blazor Server with efficient state management and UI updates.
/// </summary>
public partial class DropBearBadge : DropBearComponentBase
{
    private const int TOOLTIP_MARGIN = 10;
    private const int TOOLTIP_MAX_WIDTH = 200;
    private const int TOOLTIP_HEIGHT = 50;
    private const string JsModuleName = JsModuleNames.Utils;

    // Increased cache duration for better performance in server scenarios
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(5);

    // Use thread-safe dictionary to cache dimensions across instances with same screen size
    private static readonly ConcurrentDictionary<string, CachedDimensions> GlobalDimensionsCache = new();

    private readonly CancellationTokenSource _tooltipCts = new();
    private readonly SemaphoreSlim _tooltipSemaphore = new(1, 1);
    private readonly string _uniqueId = Guid.NewGuid().ToString("N");

    private WindowDimensions? _cachedDimensions;

    private IJSObjectReference? _jsModule;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private string _previousTooltip = string.Empty;
    private volatile bool _showTooltip;
    private string _tooltipStyle = string.Empty;

    /// <summary>
    ///     A class to hold cached window dimensions with timestamp
    /// </summary>
    private sealed class CachedDimensions
    {
        public WindowDimensions Dimensions { get; set; } = new(0, 0);
        public DateTime Timestamp { get; set; }
    }

    #region Parameters

    /// <summary>
    ///     Specifies the badge color (e.g., Default, Primary, Success, etc.).
    /// </summary>
    [Parameter]
    public BadgeColor Color { get; set; } = BadgeColor.Default;

    /// <summary>
    ///     Specifies the shape of the badge (e.g., Normal, Pill).
    /// </summary>
    [Parameter]
    public BadgeShape Shape { get; set; } = BadgeShape.Normal;

    /// <summary>
    ///     Optional icon class name (e.g., Font Awesome) to display in the badge.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    ///     Text to display inside the badge.
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Tooltip text displayed when hovering over the badge.
    /// </summary>
    [Parameter]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    ///     CSS class(es) to be added to the component
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    ///     Optional ID for the badge, if not provided a unique ID will be generated
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    #endregion

    #region Protected Properties

    /// <summary>
    ///     Indicates whether the tooltip is currently visible.
    /// </summary>
    protected bool ShowTooltip
    {
        get => _showTooltip;
        private set
        {
            if (_showTooltip == value)
            {
                return;
            }

            _showTooltip = value;

            // Use StateHasChanged queue to avoid too many renders
            if (!IsDisposed)
            {
                InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    ///     Inline style to position the tooltip.
    /// </summary>
    protected string TooltipStyle
    {
        get => _tooltipStyle;
        private set
        {
            if (_tooltipStyle == value)
            {
                return;
            }

            _tooltipStyle = value;

            // Only queue StateHasChanged if tooltip is shown
            if (_showTooltip && !IsDisposed)
            {
                InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    ///     Dynamic CSS class based on badge properties.
    /// </summary>
    protected string CssClass { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the element ID to use for the badge
    /// </summary>
    protected string ElementId => Id ?? $"badge-{_uniqueId}";

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Initialize cached values when parameters are set
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Only rebuild the CSS class if relevant parameters changed
        if (CssClass == string.Empty ||
            _previousTooltip != Tooltip)
        {
            CssClass = BuildCssClass();
            _previousTooltip = Tooltip;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            await InitializeJsModuleAsync();
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize DropBearBadge", ex);
        }
    }

    private async Task InitializeJsModuleAsync()
    {
        try
        {
            await _tooltipSemaphore.WaitAsync();
            var jsModuleResult = await GetJsModuleAsync(JsModuleName);

            if (!jsModuleResult.IsSuccess)
            {
                LogError("Failed to load JS module: {Error}", jsModuleResult.Exception);
                return;
            }

            _jsModule = jsModuleResult.Value;

            LogDebug("DropBearBadge JS module loaded successfully");
        }
        finally
        {
            _tooltipSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // await _tooltipCts.CancelAsync();
            ShowTooltip = false;
            TooltipStyle = string.Empty;
            _cachedDimensions = null;

            await base.CleanupJavaScriptResourcesAsync();
            LogDebug("DropBearBadge resources cleaned up");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Error during cleanup", ex);
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await _tooltipCts.CancelAsync();
            _tooltipCts.Dispose();
            _tooltipSemaphore.Dispose();
        }
        finally
        {
            await base.DisposeAsync();
        }
    }

    #endregion

    #region Tooltip Handlers

    /// <summary>
    ///     Shows the tooltip with optimized positioning and state management.
    /// </summary>
    protected async Task OnTooltipShow(MouseEventArgs args)
    {
        if (string.IsNullOrEmpty(Tooltip) || IsDisposed || _tooltipCts.Token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            // Use non-blocking check first to avoid unnecessary semaphore wait
            if (!await _tooltipSemaphore.WaitAsync(100, _tooltipCts.Token))
            {
                return;
            }

            // Use cached dimensions when available
            var dimensions = await GetWindowDimensionsAsync();
            if (dimensions == null)
            {
                return;
            }

            await QueueStateHasChangedAsync(() =>
            {
                CalculateTooltipPosition(args, dimensions);
                ShowTooltip = true;
            });
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation - it's expected during disposal
        }
        catch (Exception ex)
        {
            LogError("Error showing tooltip", ex);
            await QueueStateHasChangedAsync(() =>
            {
                ShowTooltip = false;
                TooltipStyle = string.Empty;
            });
        }
        finally
        {
            if (!_tooltipCts.Token.IsCancellationRequested)
            {
                _tooltipSemaphore.Release();
            }
        }
    }

    /// <summary>
    ///     Hides the tooltip with proper thread synchronization.
    /// </summary>
    protected async Task OnTooltipHide()
    {
        if (!ShowTooltip || IsDisposed)
        {
            return;
        }

        try
        {
            await QueueStateHasChangedAsync(() =>
            {
                ShowTooltip = false;
                TooltipStyle = string.Empty;
            });
        }
        catch (Exception ex)
        {
            LogError("Error hiding tooltip", ex);
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<WindowDimensions?> GetWindowDimensionsAsync()
    {
        // Check if module is initialized
        if (_jsModule == null)
        {
            try
            {
                await InitializeJsModuleAsync();
                if (_jsModule == null)
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize JS module", ex);
                return null;
            }
        }

        // Use cached dimensions if recent enough
        if (_cachedDimensions != null && DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            return _cachedDimensions;
        }

        // Check shared cache based on component ID
        var cacheKey = $"{JsRuntime.GetHashCode()}";
        if (GlobalDimensionsCache.TryGetValue(cacheKey, out var cachedEntry) &&
            DateTime.UtcNow - cachedEntry.Timestamp < DimensionsCacheDuration)
        {
            _cachedDimensions = cachedEntry.Dimensions;
            _lastDimensionsCheck = DateTime.UtcNow;
            return _cachedDimensions;
        }

        try
        {
            // Get dimensions with a timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_tooltipCts.Token);
            cts.CancelAfter(2000); // 2 second timeout for JS operation

            var dimensions = await _jsModule.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getWindowDimensions",
                cts.Token
            );

            // Update caches
            _cachedDimensions = dimensions;
            _lastDimensionsCheck = DateTime.UtcNow;

            // Add to global cache
            GlobalDimensionsCache[cacheKey] = new CachedDimensions
            {
                Dimensions = dimensions, Timestamp = DateTime.UtcNow
            };

            return dimensions;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError("Error getting window dimensions", ex);
            return null;
        }
    }

    private void CalculateTooltipPosition(MouseEventArgs args, WindowDimensions dimensions)
    {
        // Tooltip positioning optimization
        var mouseX = Math.Min(Math.Max(0, args.ClientX), dimensions.Width);
        var mouseY = Math.Min(Math.Max(0, args.ClientY), dimensions.Height);

        // Determine if tooltip should appear on left or right
        var offsetX = mouseX + TOOLTIP_MAX_WIDTH > dimensions.Width
            ? -TOOLTIP_MAX_WIDTH
            : TOOLTIP_MARGIN;

        // Determine if tooltip should appear above or below
        var offsetY = mouseY + TOOLTIP_HEIGHT > dimensions.Height
            ? -TOOLTIP_HEIGHT
            : TOOLTIP_MARGIN;

        TooltipStyle = $"left: {mouseX + offsetX}px; top: {mouseY + offsetY}px;";
    }

    private string BuildCssClass()
    {
        return string.Join(" ",
            new[]
            {
                "dropbear-badge", $"dropbear-badge-{Color.ToString().ToLowerInvariant()}",
                $"dropbear-badge-{Shape.ToString().ToLowerInvariant()}",
                string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Icon)
                    ? "dropbear-badge-icon-only"
                    : string.Empty,
                Class // Add user-provided classes
            }.Where(c => !string.IsNullOrEmpty(c)));
    }

    #endregion
}
