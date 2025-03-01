#region

using System.Linq.Expressions;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A column definition component for the <see cref="DropBearDataGrid{TItem}" />,
///     supporting sorting, filtering, custom templates, etc.
/// </summary>
/// <typeparam name="TItem">The type of the data item for the grid.</typeparam>
public sealed partial class DropBearDataGridColumn<TItem> : DropBearComponentBase where TItem : class
{
    #region Fields

    // Use "new" if DropBearComponentBase already defines a Logger.
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGridColumn<TItem>>();

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        var result = ValidateAndBuildColumn();
        if (!result.IsSuccess)
        {
            // Log the error and fail gracefully or throw based on context
            Logger.Error("{Error}", result.Error?.Message);

            if (result.Exception is InvalidOperationException ex)
            {
                throw ex; // Re-throw critical configuration errors
            }
        }
    }

    /// <summary>
    ///     Validates the column configuration and builds the column for the grid.
    /// </summary>
    /// <returns>A Result indicating success or failure with detailed error information.</returns>
    private Result<bool, DataGridError> ValidateAndBuildColumn()
    {
        try
        {
            // Validate the presence of a parent grid.
            if (ParentGrid is null)
            {
                var error = DataGridError.InvalidColumnConfiguration(
                    PropertyName, $"{GetType().Name} must be inside a {nameof(DropBearDataGrid<TItem>)}.");

                return Result<bool, DataGridError>.Failure(
                    error,
                    new InvalidOperationException(error.Message));
            }

            // Validate required parameters.
            if (string.IsNullOrWhiteSpace(PropertyName))
            {
                var error = DataGridError.InvalidColumnConfiguration(
                    "<unknown>", $"{nameof(PropertyName)} cannot be null or empty.");

                return Result<bool, DataGridError>.Failure(
                    error,
                    new InvalidOperationException(error.Message));
            }

            // We don't want to throw for missing PropertySelector if Template is provided
            if (PropertySelector is null && Template is null)
            {
                var error = DataGridError.InvalidColumnConfiguration(
                    PropertyName, $"Either {nameof(PropertySelector)} or {nameof(Template)} must be provided.");

                Logger.Warning("{Error}", error.Message);

                // Continue with a warning instead of failing completely
                return Result<bool, DataGridError>.Warning(false,error);
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                Logger.Warning("Column {PropertyName} has no {Title}. Consider providing a Title for clarity.",
                    PropertyName, nameof(Title));

                // Use PropertyName as a fallback for Title
                Title = PropertyName.Replace("_", " "); // Simple clean-up
            }

            // Build the internal column definition.
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

            // Register the column with the parent grid.
            try
            {
                ParentGrid.AddColumn(column);
                return Result<bool, DataGridError>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool, DataGridError>.Failure(
                    DataGridError.InvalidColumnConfiguration(PropertyName,
                        $"Failed to add column to grid: {ex.Message}"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            return Result<bool, DataGridError>.Failure(
                DataGridError.InvalidColumnConfiguration(
                    PropertyName ?? "<unknown>",
                    $"Unexpected error configuring column: {ex.Message}"),
                ex);
        }
    }

    #endregion

    #region Parameters and Cascading Parameters

    /// <summary>
    ///     The parent grid to which this column belongs.
    /// </summary>
    [CascadingParameter]
    private DropBearDataGrid<TItem> ParentGrid { get; set; } = null!;

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
    public Expression<Func<TItem, object>>? PropertySelector { get; set; }

    /// <summary>
    ///     If true, the user can click the header to sort by this column.
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    ///     If true, allows filtering of this column.
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    ///     The width of this column in pixels (default is 100).
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
    ///     A custom header template for advanced header markup.
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    ///     A custom footer template for advanced footer markup.
    /// </summary>
    [Parameter]
    public RenderFragment? FooterTemplate { get; set; }

    /// <summary>
    ///     A handler for errors encountered when configuring this column.
    /// </summary>
    [Parameter]
    public EventCallback<Result<bool, DataGridError>> OnColumnError { get; set; }

    #endregion
}
