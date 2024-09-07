#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

public sealed partial class SectionContainer : DropBearComponentBase
{
    private string MaxWidthStyle { get; set; } = "100%"; // Default value if not set dynamically

    /// <summary>
    ///     The content to be rendered within the container.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Specifies the maximum width of the container.
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
    ///     Determines the CSS class applied to the container.
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Call JavaScript to get the window size and calculate the max width
            await SetMaxWidthBasedOnWindowSize();

            // Register resize event listener
            var dotnetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("addResizeEventListener", dotnetRef);
        }
    }

    [JSInvokable]
    private async Task SetMaxWidthBasedOnWindowSize()
    {
        // Call the JavaScript function to get the window's width
        var dimensions = await JSRuntime.InvokeAsync<WindowDimensions>("getWindowDimensions", null);
        var windowWidth = dimensions.Width;

        // Check if the MaxWidth is a percentage (e.g., "70%") and calculate the actual width based on the window width
        if (MaxWidth != null && MaxWidth.EndsWith("%"))
        {
            var percentage = double.Parse(MaxWidth.TrimEnd('%')) / 100;
            var calculatedWidth = windowWidth * percentage;
            MaxWidthStyle = $"{calculatedWidth}px";
        }
        else
        {
            // If the MaxWidth is not a percentage (e.g., "800px"), use it directly
            MaxWidthStyle = MaxWidth ?? "100%";
        }

        // Trigger a re-render with the updated max width style
        await InvokeAsync(StateHasChanged);
    }
}
