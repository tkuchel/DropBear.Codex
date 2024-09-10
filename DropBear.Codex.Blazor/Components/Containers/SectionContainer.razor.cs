#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can center its content both vertically and horizontally.
/// </summary>
public sealed partial class SectionContainer : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SectionContainer>();

    private string MaxWidthStyle { get; set; } = "100%"; // Default value if not set dynamically

    /// <summary>
    ///     The content to be rendered within the container.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Specifies the maximum width of the container. Can be in pixels (e.g., "800px") or percentage (e.g., "70%").
    /// </summary>
    [Parameter]
    public string? MaxWidth { get; set; }

    /// <summary>
    ///     If true, the container will be horizontally centered within its parent element.
    /// </summary>
    [Parameter]
    public bool IsHorizontalCentered { get; set; }

    /// <summary>
    ///     If true, the container will be vertically centered within its parent element.
    /// </summary>
    [Parameter]
    public bool IsVerticalCentered { get; set; }

    /// <summary>
    ///     Determines the CSS class applied to the container, adding centering classes if required.
    /// </summary>
    private string ContainerClass
    {
        get
        {
            var classes = "section-container";
            if (IsHorizontalCentered)
            {
                classes += " horizontal-centered";
            }

            if (IsVerticalCentered)
            {
                classes += " vertical-centered";
            }

            return classes;
        }
    }

    /// <summary>
    ///     Runs after the component has rendered to set up window resize listeners and calculate the initial max width.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Call JavaScript to get the window size and calculate the max width
                await SetMaxWidthBasedOnWindowSize();

                // Register resize event listener
                var dotnetRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("DropBearResizeManager.initialize", dotnetRef);
                Logger.Debug("Resize event listener registered.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred during OnAfterRenderAsync.");
            }
        }
    }

    /// <summary>
    ///     Sets the maximum width of the container based on the window's width and the MaxWidth parameter.
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        try
        {
            // Call the JavaScript function to get the window's width
            var dimensions = await JSRuntime.InvokeAsync<WindowDimensions>("getWindowDimensions");
            var windowWidth = dimensions.Width;

            // Check if the MaxWidth is a percentage (e.g., "70%") and calculate the actual width based on the window width
            if (!string.IsNullOrEmpty(MaxWidth) && MaxWidth.EndsWith("%"))
            {
                if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
                {
                    var calculatedWidth = windowWidth * (percentage / 100);
                    MaxWidthStyle = $"{calculatedWidth}px";
                    Logger.Debug("MaxWidth calculated based on percentage: {MaxWidthStyle}", MaxWidthStyle);
                }
                else
                {
                    Logger.Error("Failed to parse MaxWidth as a percentage: {MaxWidth}", MaxWidth);
                    MaxWidthStyle = "100%"; // Fallback to full width
                }
            }
            else
            {
                // If the MaxWidth is not a percentage (e.g., "800px"), use it directly
                MaxWidthStyle = MaxWidth ?? "100%";
                Logger.Debug("MaxWidth set directly: {MaxWidthStyle}", MaxWidthStyle);
            }

            // Trigger a re-render with the updated max width style
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while setting max width based on window size.");
            MaxWidthStyle = "100%"; // Fallback in case of error
        }
    }
}
