#region

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
public sealed partial class ReportViewer<TItem> : DropBearComponentBase where TItem : class
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ReportViewer<TItem>>();
    private readonly ExcelExporter<TItem> _excelExporter = new();
    private readonly Dictionary<string, PropertyInfo?> _propertyCache = new();
    private const int FileSizeThreshold = 32 * 1024; // 32 KB

    /// <summary>
    ///     Gets or sets the data to display in the report viewer.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();

    private List<ColumnDefinition> Columns { get; set; } = new();

    /// <summary>
    ///     Gets the filtered and sorted data.
    /// </summary>
    private IEnumerable<TItem> FilteredData => ApplySortingAndFiltering();

    private SortDirection CurrentSortDirection { get; set; } = SortDirection.Ascending;
    private string CurrentSortColumn { get; set; } = string.Empty;

    /// <summary>
    ///     Called when the component is initialized.
    /// </summary>
    protected override void OnInitialized()
    {
        try
        {
            InitializeColumns();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred during component initialization.");
            // Handle exception appropriately, e.g., show an error message to the user
        }
    }

    /// <summary>
    ///     Initializes the columns based on the properties of <typeparamref name="TItem" />.
    /// </summary>
    private void InitializeColumns()
    {
        try
        {
            Logger.Information("Initializing columns for type {TItem}.", typeof(TItem).Name);

            Columns = typeof(TItem).GetProperties()
                .Select(prop => new ColumnDefinition
                {
                    PropertyName = prop.Name,
                    DisplayName = prop.Name, // Customize display names as needed
                    FilterValue = string.Empty
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while initializing columns.");
            throw; // Rethrow to be caught in OnInitialized
        }
    }

    /// <summary>
    ///     Handles sorting when a column header is clicked.
    /// </summary>
    /// <param name="column">The column to sort by.</param>
    private void SortBy(ColumnDefinition column)
    {
        try
        {
            if (CurrentSortColumn == column.PropertyName)
            {
                CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
            }
            else
            {
                CurrentSortColumn = column.PropertyName;
                CurrentSortDirection = SortDirection.Ascending;
            }

            Logger.Information("Sorting by column '{ColumnName}' in {SortDirection} order.", CurrentSortColumn,
                CurrentSortDirection);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while sorting by column '{ColumnName}'.", column.PropertyName);
            // Optionally handle the exception, e.g., reset sorting
        }
    }

    /// <summary>
    ///     Applies sorting and filtering to the data.
    /// </summary>
    /// <returns>The sorted and filtered data.</returns>
    private IEnumerable<TItem> ApplySortingAndFiltering()
    {
        try
        {
            var query = Data.AsQueryable();

            // Apply filtering
            foreach (var column in Columns.Where(c => !string.IsNullOrEmpty(c.FilterValue)))
            {
                try
                {
                    query = query.Where(item => GetPropertyValue(item, column.PropertyName)
                        .ToString()
                        .Contains(column.FilterValue, StringComparison.OrdinalIgnoreCase) == true);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "An error occurred while applying filter on column '{ColumnName}'.",
                        column.PropertyName);
                    // Optionally handle the exception, e.g., skip this filter
                }
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(CurrentSortColumn))
            {
                try
                {
                    query = CurrentSortDirection == SortDirection.Ascending
                        ? query.OrderBy(item => GetPropertyValue(item, CurrentSortColumn))
                        : query.OrderByDescending(item => GetPropertyValue(item, CurrentSortColumn));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "An error occurred while sorting by column '{ColumnName}'.", CurrentSortColumn);
                    // Optionally handle the exception, e.g., return unsorted data
                }
            }

            return query.ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred during sorting and filtering.");
            return Enumerable.Empty<TItem>();
        }
    }

    /// <summary>
    ///     Retrieves the value of a property from an object using reflection.
    /// </summary>
    /// <param name="obj">The object to retrieve the property value from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value, or null if not found.</returns>
    private object? GetPropertyValue(object obj, string propertyName)
    {
        try
        {
            if (!_propertyCache.TryGetValue(propertyName, out var propertyInfo))
            {
                propertyInfo = obj.GetType().GetProperty(propertyName);
                _propertyCache[propertyName] = propertyInfo;
            }

            return propertyInfo?.GetValue(obj);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while getting property '{PropertyName}' value.", propertyName);
            return null;
        }
    }


    /// <summary>
    ///     Gets the sort indicator for a column.
    /// </summary>
    /// <param name="column">The column to get the sort indicator for.</param>
    /// <returns>A markup string containing the sort indicator.</returns>
    private MarkupString GetSortIndicator(ColumnDefinition column)
    {
        if (column.PropertyName != CurrentSortColumn)
        {
            return new MarkupString(string.Empty);
        }

        var indicator = CurrentSortDirection == SortDirection.Ascending ? "▲" : "▼";
        return new MarkupString($"<span>{indicator}</span>");
    }

    /// <summary>
    ///     Exports the filtered data to an Excel file as a MemoryStream.
    /// </summary>
    private async Task ExportToExcelAsStream()
    {
        try
        {
            Logger.Information("Exporting data to Excel.");

            var ms = _excelExporter.ExportToExcelStream(FilteredData.ToList());

            if (ms == null || ms.Length == 0)
            {
                Logger.Error("Excel export resulted in an empty file.");
                // Optionally display an alert to the user
                return;
            }

            Logger.Information("Data exported to Excel successfully.");

            // Determine which method to use based on the file size
            if (ms.Length < FileSizeThreshold)
            {
                // Use byte array method for smaller files
                byte[] fileBytes = ms.ToArray();
                string base64 = Convert.ToBase64String(fileBytes);

                await Js.InvokeVoidAsync("downloadFileFromStream", "ExportedData.xlsx", base64, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }

            else
            {
                // Use DotNetStreamReference for larger files
                ms.Position = 0; // Ensure the stream is at the beginning
                using var streamRef = new DotNetStreamReference(stream: ms);

                await Js.InvokeVoidAsync("downloadFileFromStream", "ExportedData.xlsx", streamRef, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred during Excel export.");
            // Optionally display an alert to the user
        }
    }

}
