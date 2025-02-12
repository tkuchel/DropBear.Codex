#region

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
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(1);
    private readonly CancellationTokenSource _tooltipCts = new();
    private readonly SemaphoreSlim _tooltipSemaphore = new(1, 1);
    private WindowDimensions? _cachedDimensions;

    private IJSObjectReference? _jsModule;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private volatile bool _showTooltip;
    private string _tooltipStyle = string.Empty;

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
            InvokeAsync(StateHasChanged);
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
            InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    ///     Dynamic CSS class based on badge properties.
    /// </summary>
    protected string CssClass => BuildCssClass();

    #endregion

    #region Lifecycle Methods

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
            _jsModule = await GetJsModuleAsync(JsModuleName);
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
            await _tooltipCts.CancelAsync();
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
            await _tooltipSemaphore.WaitAsync(_tooltipCts.Token);

            var dimensions = await GetWindowDimensionsAsync();
            if (dimensions == null)
            {
                return;
            }

            await InvokeAsync(() =>
            {
                CalculateTooltipPosition(args, dimensions);
                ShowTooltip = true;
            });

            LogDebug("Tooltip shown at X={X}, Y={Y}", args.ClientX, args.ClientY);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogError("Error showing tooltip", ex);
            await InvokeAsync(() =>
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
        try
        {
            await InvokeAsync(() =>
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
        if (_jsModule == null)
        {
            try
            {
                await InitializeJsModuleAsync();
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

        try
        {
            _cachedDimensions = await _jsModule!.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getWindowDimensions",
                _tooltipCts.Token
            );
            _lastDimensionsCheck = DateTime.UtcNow;
            return _cachedDimensions;
        }
        catch (Exception ex)
        {
            LogError("Error getting window dimensions", ex);
            return null;
        }
    }

    private void CalculateTooltipPosition(MouseEventArgs args, WindowDimensions dimensions)
    {
        var offsetX = args.ClientX + TOOLTIP_MAX_WIDTH > dimensions.Width
            ? -TOOLTIP_MAX_WIDTH
            : TOOLTIP_MARGIN;

        var offsetY = args.ClientY + TOOLTIP_HEIGHT > dimensions.Height
            ? -TOOLTIP_HEIGHT
            : TOOLTIP_MARGIN;

        TooltipStyle = $"left: {args.ClientX + offsetX}px; top: {args.ClientY + offsetY}px;";
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
                    : string.Empty
            }.Where(c => !string.IsNullOrEmpty(c)));
    }

    #endregion
}
