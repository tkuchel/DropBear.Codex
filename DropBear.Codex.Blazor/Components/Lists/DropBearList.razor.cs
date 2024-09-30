#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Lists;

/// <summary>
///     A Blazor component that renders a list of items with an expandable/collapsible header.
/// </summary>
/// <typeparam name="T">The type of the data items in the list.</typeparam>
public sealed partial class DropBearList<T> : DropBearComponentBase where T : class
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearList<T>>();

    /// <summary>
    ///     The collection of items to render in the list.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<T>? Items { get; set; }

    /// <summary>
    ///     The title to display in the header of the list.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     An optional icon to display in the header.
    /// </summary>
    [Parameter]
    public string HeaderIcon { get; set; } = string.Empty;

    /// <summary>
    ///     The color to use for the list header. It defaults to a CSS variable for consistent styling.
    /// </summary>
    [Parameter]
    public string HeaderColor { get; set; } = "var(--clr-grey-500, #95989C)";

    /// <summary>
    ///     A template to render each item in the list.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment<T> ItemTemplate { get; set; } = null!;

    /// <summary>
    ///     Determines whether the list should be collapsed or expanded by default.
    /// </summary>
    [Parameter]
    public bool CollapsedByDefault { get; set; }

    /// <summary>
    ///     An event callback that is invoked whenever the list is collapsed or expanded.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnToggle { get; set; }

    private bool IsCollapsed { get; set; }

    protected override void OnInitialized()
    {
        // Set the initial collapsed state based on the parameter
        IsCollapsed = CollapsedByDefault;
        Logger.Debug("DropBearList initialized with {IsCollapsed} state.", IsCollapsed);

        // Validate that the ItemTemplate is provided
        if (ItemTemplate == null)
        {
            Logger.Error("ItemTemplate is required but was not provided.");
            throw new InvalidOperationException($"{nameof(ItemTemplate)} is required and cannot be null.");
        }
    }

    /// <summary>
    ///     Toggles the collapsed state of the list.
    /// </summary>
    private async Task ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
        // Logger.Debug("List {Title} collapsed state toggled to {IsCollapsed}.", Title, IsCollapsed);

        // Trigger the OnToggle callback to notify parent components of the state change
        if (OnToggle.HasDelegate)
        {
            await OnToggle.InvokeAsync(IsCollapsed);
        }
    }
}
