#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

public sealed partial class SectionContainer : DropBearComponentBase
{
    /// <summary>
    ///     The content to be rendered within the container. This can include one or more <see cref="SectionComponent" />
    ///     instances.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Specifies the maximum width of the container. This value can be any valid CSS width value (e.g., "800px", "50%").
    /// </summary>
    [Parameter]
    public string? MaxWidth { get; set; }

    /// <summary>
    ///     If true, the container will be centered within its parent element.
    /// </summary>
    [Parameter]
    public bool IsCentered { get; set; }

    /// <summary>
    ///     Determines the CSS class applied to the container. Handles centering based on the <see cref="IsCentered" />
    ///     property.
    /// </summary>
    private string ContainerClass => IsCentered ? "section-container centered" : "section-container";

    /// <summary>
    ///     Initializes the component and ensures necessary parameters are set.
    /// </summary>
    protected override void OnInitialized()
    {
        if (ChildContent is null)
        {
            throw new InvalidOperationException($"{nameof(ChildContent)} must be provided and cannot be null.");
        }

        base.OnInitialized();
    }
}
