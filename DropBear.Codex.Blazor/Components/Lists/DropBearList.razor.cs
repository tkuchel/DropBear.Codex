#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Lists;

public sealed partial class DropBearList<T> : DropBearComponentBase where T : class
{
    [Parameter] public IReadOnlyCollection<T>? Items { get; set; }
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string HeaderIcon { get; set; } = string.Empty;

    // Use a CSS variable for header color to align with the global styling approach
    [Parameter] public string HeaderColor { get; set; } = "var(--clr-grey-500, #95989C)";

    [Parameter] public RenderFragment<T> ItemTemplate { get; set; } = null!;

    private bool IsCollapsed { get; set; }

    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }
}
