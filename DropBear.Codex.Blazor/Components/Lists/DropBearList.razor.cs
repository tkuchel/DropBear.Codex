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
    #region Fields

    // Logger for this component.
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearList<T>>();

    #endregion

    #region Properties

    /// <summary>
    ///     Tracks whether the list is currently collapsed.
    /// </summary>
    private bool IsCollapsed { get; set; }

    #endregion

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Initialize collapsed state based on parameter.
        IsCollapsed = CollapsedByDefault;
        Logger.Debug("DropBearList initialized with Collapsed={Collapsed}", IsCollapsed);

        // Extra runtime validation for the required template.
        if (ItemTemplate is null)
        {
            Logger.Error("ItemTemplate is required but was not provided.");
            throw new InvalidOperationException($"{nameof(ItemTemplate)} cannot be null.");
        }
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Toggles the collapsed state of the list and invokes the OnToggle callback.
    /// </summary>
    private async Task ToggleCollapse()
    {
        // Toggle the collapsed flag.
        IsCollapsed = !IsCollapsed;
        Logger.Debug("List {Title} collapsed state toggled to {IsCollapsed}.", Title, IsCollapsed);

        // Invoke the callback if one is set.
        if (OnToggle.HasDelegate)
        {
            await OnToggle.InvokeAsync(IsCollapsed);
        }
    }

    #endregion

    #region Parameters

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
    ///     An optional icon (e.g., FontAwesome class) to display in the header.
    /// </summary>
    [Parameter]
    public string HeaderIcon { get; set; } = string.Empty;

    /// <summary>
    ///     The color to use for the list header. Defaults to a CSS variable for consistent styling.
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
    ///     If true, the list is collapsed by default on initial render.
    /// </summary>
    [Parameter]
    public bool CollapsedByDefault { get; set; }

    /// <summary>
    ///     An event callback invoked whenever the list is collapsed or expanded.
    ///     The boolean argument indicates whether the list is currently collapsed.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnToggle { get; set; }

    #endregion
}
