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
    private bool _isInitialized;

    [CascadingParameter] private DropBearDataGrid<TItem> ParentGrid { get; set; } = default!;

    /// <summary>
    ///     The property name of the data item to be displayed in this column.
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
    ///     Whether the column can be sorted. Defaults to true.
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; } = true;

    /// <summary>
    ///     Whether the column can be filtered. Defaults to true.
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; } = true;

    /// <summary>
    ///     A format string for displaying the column's values, if applicable (e.g., dates or numbers).
    /// </summary>
    [Parameter]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    ///     The width of the column, in pixels. Defaults to 150px.
    /// </summary>
    [Parameter]
    public int Width { get; set; } = 150;

    /// <summary>
    ///     A template for custom rendering of the column's data.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? Template { get; set; }

    /// <summary>
    ///     A custom sorting function that can be provided for custom sorting behavior.
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    protected override void OnInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            // Ensure the component is being used within a DropBearDataGrid context
            if (ParentGrid is null)
            {
                Logger.Error(
                    $"{nameof(DropBearDataGridColumn<TItem>)} must be used within a {nameof(DropBearDataGrid<TItem>)} component.");
                throw new InvalidOperationException(
                    $"{nameof(DropBearDataGridColumn<TItem>)} must be used within a {nameof(DropBearDataGrid<TItem>)}.");
            }

            // Create a new column definition using constructor parameters
            var column = new DataGridColumn<TItem>(
                PropertyName,
                Title,
                Sortable,
                Filterable,
                Width,
                string.Empty, // Use a default or customize as needed
                true,
                Format
            ) { PropertySelector = PropertySelector, Template = Template, CustomSort = CustomSort };

            // Add the column to the parent grid
            ParentGrid.AddColumn(column);
            // Logger.Debug("Column {PropertyName} added to the DropBearDataGrid.", PropertyName);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while initializing the DropBearDataGridColumn.");
            throw;
        }
    }
}
