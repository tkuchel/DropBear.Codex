#region

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Exporters;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Reports;

/// <summary>
///     A Blazor component that displays data in a table with sorting, filtering, and export functionality.
/// </summary>
/// <typeparam name="TItem">The type of data items to display.</typeparam>
public sealed partial class DropBearReportViewer<TItem> : DropBearComponentBase where TItem : class
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearReportViewer<TItem>>();
    private readonly ExcelExporter<TItem> _excelExporter = new();
    private List<ColumnDefinition<TItem>> _columns = new();
    private ColumnDefinition<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;

    /// <summary>
    ///     The dataset to display in the report viewer.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> Data { get; set; } = [];

    /// <summary>
    ///     Optionally provide specific columns to be rendered; otherwise columns are auto-generated.
    /// </summary>
    [Parameter]
    public List<ColumnDefinition<TItem>>? ColumnDefinitions { get; set; }

    /// <summary>
    ///     The columns used in the report, prioritizing <see cref="ColumnDefinitions" /> over auto-generated ones.
    /// </summary>
    private IEnumerable<ColumnDefinition<TItem>> ResolvedColumns =>
        ColumnDefinitions is { Count: > 0 } ? ColumnDefinitions : _columns;

    /// <summary>
    ///     The filtered (and sorted) data to display.
    /// </summary>
    private IEnumerable<TItem> FilteredData => ApplySortingAndFiltering();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        InitializeColumns();
    }

    /// <summary>
    ///     Auto-generates columns from the TItem's properties unless <see cref="ColumnDefinitions" /> is provided.
    /// </summary>
    private void InitializeColumns()
    {
        Logger.Debug("Initializing columns for type {TItem}.", typeof(TItem).Name);

        _columns = typeof(TItem).GetProperties()
            .Select(prop => new ColumnDefinition<TItem>
            {
                PropertyName = prop.Name,
                DisplayName = prop.Name,
                IsVisible = true,
                FilterValue = string.Empty,
                PropertySelector = CreatePropertySelector(prop)
            })
            .ToList();
    }

    /// <summary>
    ///     Creates a property selector expression for a given <see cref="PropertyInfo" />.
    /// </summary>
    private static Expression<Func<TItem, object>> CreatePropertySelector(PropertyInfo propertyInfo)
    {
        var parameter = Expression.Parameter(typeof(TItem), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var convert = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<TItem, object>>(convert, parameter);
    }

    /// <summary>
    ///     Toggles sorting direction or sets a new column as the current sort column.
    /// </summary>
    private void SortBy(ColumnDefinition<TItem> column)
    {
        if (_currentSortColumn == column)
        {
            _currentSortDirection = _currentSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _currentSortColumn = column;
            _currentSortDirection = SortDirection.Ascending;
        }

        Logger.Debug("Sorting by column '{ColumnName}' in {SortDirection} order.",
            column.PropertyName, _currentSortDirection);
    }

    /// <summary>
    ///     Filters and sorts the data based on the selected columns and filter values.
    /// </summary>
    private IEnumerable<TItem> ApplySortingAndFiltering()
    {
        var query = Data.AsQueryable();

        // Apply filtering
        foreach (var column in ResolvedColumns.Where(c => !string.IsNullOrWhiteSpace(c.FilterValue)))
        {
            var filterValue = column.FilterValue;
            var parameter = Expression.Parameter(typeof(TItem), "x");
            var propertyAccess = Expression.Invoke(column.PropertySelector, parameter);
            var toStringCall = Expression.Call(propertyAccess, "ToString", Type.EmptyTypes);

            var containsMethod = typeof(string).GetMethod(nameof(string.Contains),
                [typeof(string), typeof(StringComparison)]);
            var comparisonType = Expression.Constant(StringComparison.OrdinalIgnoreCase, typeof(StringComparison));

            var containsCall = Expression.Call(
                toStringCall,
                containsMethod!,
                Expression.Constant(filterValue),
                comparisonType);

            var lambda = Expression.Lambda<Func<TItem, bool>>(containsCall, parameter);
            query = query.Where(lambda);
        }

        // Apply sorting
        if (_currentSortColumn != null)
        {
            query = _currentSortDirection == SortDirection.Ascending
                ? query.OrderBy(_currentSortColumn.PropertySelector)
                : query.OrderByDescending(_currentSortColumn.PropertySelector);
        }

        return query.ToList();
    }

    /// <summary>
    ///     Formats a property's value for display, including custom date handling.
    /// </summary>
    private string GetFormattedValue(TItem item, Expression<Func<TItem, object>> propertySelector)
    {
        var value = propertySelector.Compile()(item);
        if (value == null)
        {
            return string.Empty;
        }

        if (value is DateTime dateTimeValue)
        {
            // Format dates in AU format: dd/MM/yyyy
            return dateTimeValue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (value is string strValue)
        {
            // Example special case: mobile numbers
            if (propertySelector.Body is MemberExpression memberExpr &&
                memberExpr.Member.Name.Contains("MobileNumber") &&
                !strValue.StartsWith("0") && strValue.Length == 9)
            {
                return $"0{strValue}";
            }
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Shows a sort indicator (▲ or ▼) if the column is currently being sorted, otherwise empty.
    /// </summary>
    private MarkupString GetSortIndicator(ColumnDefinition<TItem> column)
    {
        if (_currentSortColumn != column)
        {
            return new MarkupString(string.Empty);
        }

        var indicator = _currentSortDirection == SortDirection.Ascending ? "▲" : "▼";
        return new MarkupString($"<span>{indicator}</span>");
    }

    /// <summary>
    ///     Exports the currently filtered dataset to Excel.
    /// </summary>
    private async Task ExportToExcelAsync()
    {
        Logger.Debug("Exporting data to Excel.");

        var dataToExport = FilteredData.ToList();
        using var ms = _excelExporter.ExportToExcelStream(dataToExport);

        if (ms == null || ms.Length == 0)
        {
            Logger.Error("Excel export resulted in an empty file.");
            return;
        }

        Logger.Debug("Data exported to Excel successfully.");

        ms.Position = 0;

        // Use a DotNetStreamReference for JS-based download
        using var streamRef = new DotNetStreamReference(ms);
        await JsRuntime.InvokeVoidAsync(
            "downloadFileFromStream",
            "ExportedData.xlsx",
            streamRef,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
