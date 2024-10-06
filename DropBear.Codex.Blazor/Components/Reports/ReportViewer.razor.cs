#region

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Utilities.Exporters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Reports;

/// <summary>
///     A Blazor component that displays data in a table with sorting, filtering, and export functionality.
/// </summary>
/// <typeparam name="TItem">The type of data items to display.</typeparam>
public sealed partial class ReportViewer<TItem> : DropBearComponentBase where TItem : class
{
    private const int FileSizeThreshold = 32 * 1024; // 32 KB

    private readonly ExcelExporter<TItem> _excelExporter = new();
    private List<ColumnDefinition<TItem>> _columns = new();
    private ColumnDefinition<TItem>? _currentSortColumn;

    private SortDirection _currentSortDirection = SortDirection.Ascending;

    [Inject] private ILogger<ReportViewer<TItem>> Logger { get; set; } = default!;

    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the data to display in the report viewer.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();

    /// <summary>
    ///     Optionally provide specific columns to be rendered.
    /// </summary>
    [Parameter]
    public List<ColumnDefinition<TItem>>? ColumnDefinitions { get; set; }

    /// <summary>
    ///     Gets the columns used in the report, prioritizing custom definitions over auto-generated ones.
    /// </summary>
    private IEnumerable<ColumnDefinition<TItem>> ResolvedColumns =>
        ColumnDefinitions?.Any() == true ? ColumnDefinitions : _columns;

    /// <summary>
    ///     Gets the filtered and sorted data.
    /// </summary>
    private IEnumerable<TItem> FilteredData => ApplySortingAndFiltering();

    protected override void OnInitialized()
    {
        InitializeColumns();
    }

    private void InitializeColumns()
    {
        Logger.LogDebug("Initializing columns for type {TItem}.", typeof(TItem).Name);

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

    private static Expression<Func<TItem, object>> CreatePropertySelector(PropertyInfo propertyInfo)
    {
        var parameter = Expression.Parameter(typeof(TItem), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var convert = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<TItem, object>>(convert, parameter);
    }

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

        Logger.LogDebug("Sorting by column '{ColumnName}' in {SortDirection} order.", column.PropertyName,
            _currentSortDirection);
    }

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
                new[] { typeof(string), typeof(StringComparison) });
            var comparisonType = Expression.Constant(StringComparison.OrdinalIgnoreCase, typeof(StringComparison));
            var containsCall = Expression.Call(toStringCall, containsMethod!, Expression.Constant(filterValue),
                comparisonType);
            var lambda = Expression.Lambda<Func<TItem, bool>>(containsCall, parameter);
            query = query.Where(lambda);
        }

        // Apply sorting
        if (_currentSortColumn != null)
        {
            if (_currentSortDirection == SortDirection.Ascending)
            {
                query = query.OrderBy(_currentSortColumn.PropertySelector);
            }
            else
            {
                query = query.OrderByDescending(_currentSortColumn.PropertySelector);
            }
        }

        return query.ToList();
    }

    private string GetFormattedValue(TItem item, Expression<Func<TItem, object>> propertySelector)
    {
        var value = propertySelector.Compile()(item);
        if (value == null)
        {
            return string.Empty;
        }

        if (value is DateTime dateTimeValue)
        {
            // Format the date in AU format: dd/MM/yyyy
            return dateTimeValue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (value is string strValue)
        {
            // Handle special cases like mobile numbers
            if (propertySelector.Body is MemberExpression memberExpr &&
                memberExpr.Member.Name.Contains("MobileNumber") &&
                !strValue.StartsWith("0") && strValue.Length == 9)
            {
                return $"0{strValue}";
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private MarkupString GetSortIndicator(ColumnDefinition<TItem> column)
    {
        if (_currentSortColumn != column)
        {
            return new MarkupString(string.Empty);
        }

        var indicator = _currentSortDirection == SortDirection.Ascending ? "▲" : "▼";
        return new MarkupString($"<span>{indicator}</span>");
    }

    private async Task ExportToExcelAsync()
    {
        Logger.LogDebug("Exporting data to Excel.");

        var dataToExport = FilteredData.ToList();
        using var ms = _excelExporter.ExportToExcelStream(dataToExport);

        if (ms == null || ms.Length == 0)
        {
            Logger.LogError("Excel export resulted in an empty file.");
            return;
        }

        Logger.LogDebug("Data exported to Excel successfully.");

        ms.Position = 0;

        using var streamRef = new DotNetStreamReference(ms);

        await JsRuntime.InvokeVoidAsync("downloadFileFromStream", "ExportedData.xlsx", streamRef,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
