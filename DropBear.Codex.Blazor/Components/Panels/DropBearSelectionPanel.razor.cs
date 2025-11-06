#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Panels;

public partial class DropBearSelectionPanel<T> : DropBearComponentBase
{
    /// <summary>
    ///     The collection of items selected from the DataGrid
    /// </summary>
    [Parameter]
    public required List<T> SelectedItems { get; set; }

    /// <summary>
    ///     Event callback that is triggered when an item is removed from the selection
    /// </summary>
    [Parameter]
    public EventCallback<T> OnItemRemoved { get; set; }

    /// <summary>
    ///     Event callback that is triggered when the selection changes
    /// </summary>
    [Parameter]
    public EventCallback<List<T>> OnSelectionChanged { get; set; }

    /// <summary>
    ///     Title displayed at the top of the selection panel
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Selected Items";

    /// <summary>
    ///     Text displayed when no items are selected
    /// </summary>
    [Parameter]
    public string EmptySelectionText { get; set; } = "No items selected";

    /// <summary>
    ///     List of action buttons (maximum of 3) to display
    /// </summary>
    [Parameter]
    public List<ActionButton<T>>? ActionButtons { get; set; }

    /// <summary>
    ///     Optional function to determine how to display each item
    /// </summary>
    [Parameter]
    public Func<T, string>? ItemDisplayExpression { get; set; }

    /// <summary>
    ///     Optional template for customizing how items are displayed
    /// </summary>
    [Parameter]
    public RenderFragment<T>? ItemTemplate { get; set; }

    /// <summary>
    ///     Determines if the panel is initially collapsed
    /// </summary>
    [Parameter]
    public bool InitiallyCollapsed { get; set; }

    /// <summary>
    ///     CSS class to be applied to the component
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Gets whether the panel is currently collapsed
    /// </summary>
    public bool IsCollapsed { get; private set; }

    /// <summary>
    ///     Initializes the component
    /// </summary>
    protected override void OnInitialized()
    {
        IsCollapsed = InitiallyCollapsed;

        base.OnInitialized();
    }

    /// <summary>
    ///     Toggles the collapsed state of the panel
    /// </summary>
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
        StateHasChanged();
    }

    /// <summary>
    ///     Removes an item from the selected items
    /// </summary>
    /// <param name="item">The item to remove</param>
    public async Task RemoveItem(T item)
    {
        if (SelectedItems != null && SelectedItems.Contains(item))
        {
            SelectedItems.Remove(item);

            if (OnItemRemoved.HasDelegate)
            {
                await OnItemRemoved.InvokeAsync(item);
            }

            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync(SelectedItems);
            }

            StateHasChanged();
        }
    }

    /// <summary>
    ///     Clears all selected items
    /// </summary>
    public async Task ClearSelection()
    {
        if (SelectedItems != null && SelectedItems.Any())
        {
            SelectedItems.Clear();

            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync(SelectedItems);
            }

            StateHasChanged();
        }
    }
}

/// <summary>
///     Represents an action button in the selection panel
/// </summary>
/// <typeparam name="T">The type of items in the selection</typeparam>
public class ActionButton<T>
{
    /// <summary>
    ///     The text to display on the button
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    ///     Optional CSS class for an icon (e.g., Font Awesome)
    /// </summary>
    public string? IconClass { get; set; }

    /// <summary>
    ///     CSS class to apply to the button
    /// </summary>
    public string ButtonClass { get; set; } = "btn-primary";

    /// <summary>
    ///     Whether the button is disabled
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    ///     The action to execute when the button is clicked
    /// </summary>
    public EventCallback<IList<T>> OnClick { get; set; }
}
