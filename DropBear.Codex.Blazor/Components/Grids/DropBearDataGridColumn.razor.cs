#region

using System.Linq.Expressions;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A column definition component for the DropBearDataGrid, supporting sorting, filtering, templates, and more.
/// </summary>
/// <typeparam name="TItem">The type of the data item for the grid.</typeparam>
public sealed partial class DropBearDataGridColumn<TItem> : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGridColumn<TItem>>();

    [CascadingParameter] private DropBearDataGrid<TItem> ParentGrid { get; set; } = default!;

    /// <summary>
    ///     The name of the property that the column represents.
    /// </summary>
    [Parameter]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    ///     The title of the column, displayed as the column header.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     An expression to select the property from the data item to be displayed in this column.
    /// </summary>
    [Parameter]
    public Expression<Func<TItem, object>> PropertySelector { get; set; } = default!;

    /// <summary>
    ///     Whether the column can be sorted. Defaults to false.
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    ///     Whether the column can be filtered. Defaults to false.
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    ///     The width of the column. Defaults to 100.
    /// </summary>
    [Parameter]
    public double Width { get; set; } = 100;

    /// <summary>
    ///     The CSS class to apply to the column.
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates if the column is visible. Defaults to true.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     A format string for displaying the column's values, if applicable.
    /// </summary>
    [Parameter]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    ///     A template for custom rendering of the column's data.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? Template { get; set; }

    /// <summary>
    ///     A custom sorting function for the column.
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    /// <summary>
    ///     A custom filter function for the column.
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, string, IEnumerable<TItem>>? CustomFilter { get; set; }

    /// <summary>
    ///     A template for rendering the column header.
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    ///     A template for rendering the column footer.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterTemplate { get; set; }

    protected override void OnInitialized()
    {
        // Ensure the component is used within a DropBearDataGrid context
        if (ParentGrid is null)
        {
            Logger.Error(
                $"{nameof(DropBearDataGridColumn<TItem>)} must be used within a {nameof(DropBearDataGrid<TItem>)} component.");
            throw new InvalidOperationException(
                $"{nameof(DropBearDataGridColumn<TItem>)} must be used within a {nameof(DropBearDataGrid<TItem>)}.");
        }

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(PropertyName))
        {
            Logger.Error($"{nameof(PropertyName)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
            throw new InvalidOperationException(
                $"{nameof(PropertyName)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
        }

        if (PropertySelector is null)
        {
            Logger.Error($"{nameof(PropertySelector)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
            throw new InvalidOperationException(
                $"{nameof(PropertySelector)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            Logger.Warning(
                $"{nameof(Title)} is empty for {nameof(DropBearDataGridColumn<TItem>)}. Consider providing a title for the column.");
        }

        // Create a new column definition using the DataGridColumn<TItem> model class
        var column = new DataGridColumn<TItem>(
            PropertyName,
            Title,
            Sortable,
            Filterable,
            Width,
            CssClass,
            Visible,
            Format
        )
        {
            PropertySelector = PropertySelector,
            Template = Template,
            CustomSort = CustomSort,
            CustomFilter = CustomFilter,
            HeaderTemplate = HeaderTemplate,
            FooterTemplate = FooterTemplate
        };

        // Add the column to the parent grid
        ParentGrid.AddColumn(column);

        // Logger.Debug("Column '{Title}' added to the DropBearDataGrid.", Title);
    }
}
