#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

public sealed partial class SectionComponent : DropBearComponentBase
{
    /// <summary>
    ///     A predicate function that determines whether the section should be rendered.
    ///     If the predicate is null or returns true, the section will be rendered.
    /// </summary>
    [Parameter]
    public Func<bool>? Predicate { get; set; }

    /// <summary>
    ///     The content to be rendered within the section.
    /// </summary>
    [Parameter, EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Validates the component's parameters and initializes necessary properties.
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
