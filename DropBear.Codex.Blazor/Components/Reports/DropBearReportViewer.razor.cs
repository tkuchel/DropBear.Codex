#region

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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
///     Optimized for Blazor Server with efficient rendering and data handling.
/// </summary>
/// <typeparam name="TItem">The type of data items to display.</typeparam>
public sealed partial class DropBearReportViewer<TItem> : DropBearComponentBase where TItem : class
{
    #region Export Functionality

    /// <summary>
    ///     Exports the currently filtered dataset to Excel.
    /// </summary>
    private async Task ExportToExcelAsync()
    {
        Logger.Debug("Exporting data to Excel.");

        if (_downloadModule == null)
        {
            Logger.Error("Download module not initialized");
            return;
        }

        var dataToExport = FilteredData.ToList();
        var exportStreamResult = await _excelExporter.ExportToExcelStreamAsync(dataToExport);

        if (exportStreamResult.IsFailure)
        {
            Logger.Error("Failed to export data to Excel: {ErrorMessage}", exportStreamResult.Error.Message);
            return;
        }

        using var ms = exportStreamResult.Value;

        if (ms.Length == 0)
        {
            Logger.Error("Excel export resulted in an empty file.");
            return;
        }

        Logger.Debug("Data exported to Excel successfully.");
        ms.Position = 0;

        // Use a DotNetStreamReference for JavaScript-based file download.
        using var streamRef = new DotNetStreamReference(ms);

        try
        {
            // Directly invoke the method from the module reference
            await _downloadModule.InvokeVoidAsync(
                "downloadFileFromStream", // No need for "DropBearFileDownloader." prefix
                "ExportedData.xlsx",
                streamRef,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            Logger.Debug("File download initiated successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to invoke download function");
        }
    }

    #endregion

    #region Private Fields & Dependencies

    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearReportViewer<TItem>>();
    private readonly ExcelExporter<TItem> _excelExporter = new();
    private List<ColumnDefinition<TItem>> _columns = new();
    private ColumnDefinition<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;

    // Backing fields for parameters
    private IEnumerable<TItem> _data = Array.Empty<TItem>();
    private List<ColumnDefinition<TItem>>? _columnDefinitions;

    // Cache for filtered data
    private List<TItem>? _filteredDataCache;
    private bool _filteredDataDirty = true;

    // Flag to track if component should render
    private bool _shouldRender = true;

    // Semaphore for thread safety
    private readonly SemaphoreSlim _dataSemaphore = new(1, 1);
    private IJSObjectReference? _downloadModule;
    private bool _isModuleInitialized;

    #endregion

    #region Parameters

    /// <summary>
    ///     The dataset to display in the report viewer.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> Data
    {
        get => _data;
        set
        {
            if (_data != value)
            {
                _data = value;
                _filteredDataDirty = true;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Optionally provide specific columns to be rendered; otherwise columns are auto-generated.
    /// </summary>
    [Parameter]
    public List<ColumnDefinition<TItem>>? ColumnDefinitions
    {
        get => _columnDefinitions;
        set
        {
            if (_columnDefinitions != value)
            {
                _columnDefinitions = value;
                _shouldRender = true;
            }
        }
    }

    #endregion

    #region Derived Properties

    /// <summary>
    ///     The columns used in the report, prioritizing <see cref="ColumnDefinitions" /> over auto-generated ones.
    /// </summary>
    private IEnumerable<ColumnDefinition<TItem>> ResolvedColumns =>
        _columnDefinitions is { Count: > 0 } ? _columnDefinitions : _columns;

    /// <summary>
    ///     The filtered (and sorted) data to display.
    /// </summary>
    private IEnumerable<TItem> FilteredData
    {
        get
        {
            if (_filteredDataCache == null || _filteredDataDirty)
            {
                _filteredDataCache = ApplySortingAndFiltering().ToList();
                _filteredDataDirty = false;
            }

            return _filteredDataCache;
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Initializes the component by generating columns if needed.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Load the module first
            _downloadModule = await GetJsModuleAsync("DropBearFileDownloader");

            // Give the module a moment to register in the global scope
            await Task.Delay(50);

            // Now check if it's initialized, but use the module reference directly
            _isModuleInitialized = await _downloadModule.InvokeAsync<bool>("isInitialized");

            if (!_isModuleInitialized)
            {
                // Initialize through the module reference, not global scope
                await _downloadModule.InvokeVoidAsync("initialize");
                _isModuleInitialized = true;
            }

            InitializeColumns();
            await base.OnInitializedAsync();
            LogDebug("Alert container initialized with JS module");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize file downloader JS module", ex);
        }
    }

    /// <summary>
    ///     Disposes of resources used by the component.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            _dataSemaphore.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing report viewer");
        }

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Column Initialization

    /// <summary>
    ///     Auto-generates columns from the TItem's properties unless <see cref="ColumnDefinitions" /> is provided.
    /// </summary>
    private void InitializeColumns()
    {
        Logger.Debug("Initializing columns for type {TItem}.", typeof(TItem).Name);

        // Only initialize if columns haven't been generated yet
        if (_columns.Count == 0)
        {
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
    }

    /// <summary>
    ///     Creates a property selector expression for a given <see cref="PropertyInfo" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression<Func<TItem, object>> CreatePropertySelector(PropertyInfo propertyInfo)
    {
        var parameter = Expression.Parameter(typeof(TItem), "x");
        var propertyAccess = Expression.Property(parameter, propertyInfo);
        var convert = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<TItem, object>>(convert, parameter);
    }

    #endregion

    #region Sorting & Filtering

    /// <summary>
    ///     Toggles sorting direction or sets a new column as the current sort column.
    /// </summary>
    /// <param name="column">The column to sort by.</param>
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

        // Mark the filter cache as dirty and trigger a render
        _filteredDataDirty = true;
        _shouldRender = true;
    }

    /// <summary>
    ///     Filters and sorts the data based on the selected columns and filter values.
    /// </summary>
    /// <returns>A filtered and sorted enumerable of items.</returns>
    private IEnumerable<TItem> ApplySortingAndFiltering()
    {
        // Get a thread-safe lock before processing data
        _dataSemaphore.Wait();

        try
        {
            var query = _data.AsQueryable();

            // Apply filtering: for each column with a non-empty filter value, add a Where clause.
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

            // Apply sorting if a sort column is selected.
            if (_currentSortColumn != null)
            {
                query = _currentSortDirection == SortDirection.Ascending
                    ? query.OrderBy(_currentSortColumn.PropertySelector)
                    : query.OrderByDescending(_currentSortColumn.PropertySelector);
            }

            return query.ToList();
        }
        finally
        {
            _dataSemaphore.Release();
        }
    }

    /// <summary>
    ///     Marks the filtered data as dirty when any column's filter value changes.
    /// </summary>
    /// <param name="filterValue">The new filter value.</param>
    private void OnFilterValueChanged(string filterValue)
    {
        _filteredDataDirty = true;
        _shouldRender = true;
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    ///     Formats a property's value for display, including custom date handling.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <param name="propertySelector">The property selector expression.</param>
    /// <returns>A formatted string representation of the property value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetFormattedValue(TItem item, Expression<Func<TItem, object>> propertySelector)
    {
        var value = propertySelector.Compile()(item);

        if (value is null)
        {
            return string.Empty;
        }

        if (value is DateTime dateTimeValue)
        {
            // Format dates in AU format: dd/MM/yyyy.
            return dateTimeValue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (value is string strValue)
        {
            // Special handling for mobile numbers: prepend '0' if necessary.
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
    ///     Shows a sort indicator (▲ or ▼) if the column is currently being sorted.
    /// </summary>
    /// <param name="column">The column to check.</param>
    /// <returns>A MarkupString containing the sort indicator or an empty string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarkupString GetSortIndicator(ColumnDefinition<TItem> column)
    {
        if (_currentSortColumn != column)
        {
            return new MarkupString(string.Empty);
        }

        var indicator = _currentSortDirection == SortDirection.Ascending ? "▲" : "▼";
        return new MarkupString($"<span>{indicator}</span>");
    }

    #endregion
}
