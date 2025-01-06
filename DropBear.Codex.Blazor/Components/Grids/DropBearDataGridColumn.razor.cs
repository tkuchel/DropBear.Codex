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
///     A column definition component for the <see cref="DropBearDataGrid{TItem}" />, supporting sorting, filtering,
///     templates, etc.
/// </summary>
/// <typeparam name="TItem">The type of the data item for the grid.</typeparam>
public sealed partial class DropBearDataGridColumn<TItem> : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGridColumn<TItem>>();

    [CascadingParameter] private DropBearDataGrid<TItem> ParentGrid { get; set; } = null!;

    /// <summary>
    ///     The property name that this column represents (e.g., "FirstName").
    /// </summary>
    [Parameter]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    ///     The title text displayed in the column header (e.g., "First Name").
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     An expression selecting which property of <typeparamref name="TItem" /> this column binds to.
    /// </summary>
    [Parameter]
    public Expression<Func<TItem, object>> PropertySelector { get; set; } = null!;

    /// <summary>
    ///     If true, the user can click the header to sort by this column.
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    ///     If true, allows filtering of this column (not fully implemented in the example).
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    ///     The width of this column in pixels (default 100).
    /// </summary>
    [Parameter]
    public double Width { get; set; } = 100;

    /// <summary>
    ///     Additional CSS classes to apply to this column.
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     If false, the column is hidden from view.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     A format string (e.g., "dd/MM/yyyy") for date/time formatting or other IFormattable usage.
    /// </summary>
    [Parameter]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    ///     A template for custom rendering of this column's cells.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? Template { get; set; }

    /// <summary>
    ///     A custom sorting function if the default property-based sorting isn't sufficient.
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    /// <summary>
    ///     A custom filter function if default property-based filtering isn't sufficient.
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, string, IEnumerable<TItem>>? CustomFilter { get; set; }

    /// <summary>
    ///     A custom header template if you want advanced markup in the column header.
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    ///     A custom footer template if you want advanced markup in the column footer.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterTemplate { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (ParentGrid is null)
        {
            Logger.Error($"{nameof(DropBearDataGridColumn<TItem>)} must be inside a DropBearDataGrid component.");
            throw new InvalidOperationException(
                $"{nameof(DropBearDataGridColumn<TItem>)} must be inside a {nameof(DropBearDataGrid<TItem>)}."
            );
        }

        if (string.IsNullOrWhiteSpace(PropertyName))
        {
            Logger.Error($"{nameof(PropertyName)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
            throw new InvalidOperationException($"{nameof(PropertyName)} cannot be null or empty.");
        }

        if (PropertySelector is null)
        {
            Logger.Error($"{nameof(PropertySelector)} is required for {nameof(DropBearDataGridColumn<TItem>)}.");
            throw new InvalidOperationException($"{nameof(PropertySelector)} cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            Logger.Warning("Column {PropertyName} has no Title. Consider providing a Title for clarity.", PropertyName);
        }

        // Build the internal column definition
        var column = new DataGridColumn<TItem>(
            PropertyName,
            Title,
            Sortable,
            Filterable,
            Width,
            CssClass,
            Visible,
            Format)
        {
            PropertySelector = PropertySelector,
            Template = Template,
            CustomSort = CustomSort,
            CustomFilter = CustomFilter,
            HeaderTemplate = HeaderTemplate,
            FooterTemplate = FooterTemplate
        };

        // Register with the parent grid
        ParentGrid.AddColumn(column);
    }
}
