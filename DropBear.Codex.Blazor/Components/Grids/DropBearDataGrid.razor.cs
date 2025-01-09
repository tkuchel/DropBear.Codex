#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Serilog;
using Timer = System.Threading.Timer;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A Blazor component for rendering a data grid with sorting, searching, and pagination capabilities.
/// </summary>
/// <typeparam name="TItem">The type of the data items.</typeparam>
public sealed partial class DropBearDataGrid<TItem> : DropBearComponentBase, IDisposable where TItem : class
{
    private const int DebounceDelay = 300; // ms
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    private readonly List<DataGridColumn<TItem>> _columns = new();
    private readonly Dictionary<string, Func<TItem, object>> _compiledSelectors = new();
    private readonly DataGridMetricsService _metricsService = new();
    private readonly int _parallelThreshold = 500; // Number of items above which we use parallel processing
    private readonly ConditionalWeakTable<TItem, Dictionary<string, string>> _searchableValues = new();
    private readonly ConcurrentDictionary<string, string[]> _searchTermCache = new();
    private readonly List<TItem> _selectedItems = new();

    private DataGridColumn<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private Timer? _debounceTimer;
    private bool _isInitialized;
    private bool _isSearching;
    private IEnumerable<TItem>? _previousItems = [];
    private ElementReference _searchInput;

    [Parameter] public IEnumerable<TItem>? Items { get; set; } = [];
    [Parameter] public string Title { get; set; } = "Data Grid";
    [Parameter] public bool EnableSearch { get; set; } = true;
    [Parameter] public bool EnablePagination { get; set; } = true;
    [Parameter] public int ItemsPerPage { get; set; } = 10;
    [Parameter] public bool EnableMultiSelect { get; set; }
    [Parameter] public bool AllowAdd { get; set; } = true;
    [Parameter] public bool AllowEdit { get; set; } = true;
    [Parameter] public bool AllowDelete { get; set; } = true;
    [Parameter] public bool AllowExport { get; set; }
    [Parameter] public bool EnableDebugMode { get; set; }
    [Parameter] public EventCallback OnExportData { get; set; }
    [Parameter] public EventCallback OnAddItem { get; set; }
    [Parameter] public EventCallback<TItem> OnEditItem { get; set; }
    [Parameter] public EventCallback<TItem> OnDeleteItem { get; set; }
    [Parameter] public EventCallback<List<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClicked { get; set; }
    [Parameter] public EventCallback<TItem> OnRowDoubleClicked { get; set; }
    [Parameter] public RenderFragment Columns { get; set; } = null!;
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }

    private string SearchTerm { get; set; } = string.Empty;
    private int CurrentPage { get; set; } = 1;
    private IEnumerable<TItem> FilteredItems { get; set; } = new List<TItem>();
    private IEnumerable<TItem> DisplayedItems { get; set; } = new List<TItem>();
    private bool ShowMetrics => EnableDebugMode && _metricsService.IsEnabled;

    private IReadOnlyCollection<int> ItemsPerPageOptions { get; } =
        new ReadOnlyCollection<int>([10, 25, 50, 100]);

    private bool IsLoading { get; set; } = true;
    private bool HasData => FilteredItems.Any();

    private int TotalPages =>
        (int)Math.Ceiling(FilteredItems.Count() / (double)ItemsPerPage);

    private int TotalColumnCount =>
        _columns.Count + (EnableMultiSelect ? 1 : 0) + (AllowEdit || AllowDelete ? 1 : 0);

    /// <summary>
    ///     Returns a read-only list of the columns configured for this data grid.
    /// </summary>
    public IReadOnlyList<DataGridColumn<TItem>> GetColumns => _columns.AsReadOnly();

    /// <summary>
    ///     Handles cleanup of resources.
    /// </summary>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _searchableValues.Clear();
        _searchTermCache.Clear();
        _compiledSelectors.Clear();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // Reload data if the Items reference has changed
        if (!ReferenceEquals(_previousItems, Items))
        {
            _previousItems = Items;
            await LoadDataAsync();
        }

        _isInitialized = true;
    }

    /// <summary>
    ///     Handles the data loading process with performance optimizations.
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            _metricsService.IsEnabled = EnableDebugMode;
            _metricsService.StartSearchTimer();

            StateHasChanged();

            // Clear caches when loading new data
            _searchableValues.Clear();
            _searchTermCache.Clear();

            FilteredItems = Items?.ToList() ?? new List<TItem>();

            // Pre-compute searchable values for all items
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    if (!_searchableValues.TryGetValue(item, out _))
                    {
                        _searchableValues.Add(item, PreComputeSearchableValues(item));
                    }
                }
            }

            UpdateDisplayedItems();

            _metricsService.StopSearchTimer(
                Items?.Count() ?? 0,
                FilteredItems.Count(),
                DisplayedItems.Count());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading data in DropBearDataGrid.");
            _metricsService.Reset();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Called whenever the user types in the search box. Debounces the search call.
    /// </summary>
    private void OnSearchInput(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async void (_) =>
        {
            try
            {
                await DebounceSearchAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during search debounce in DropBearDataGrid.");
            }
        }, null, DebounceDelay, Timeout.Infinite);
    }

    /// <summary>
    ///     Pre-computes searchable values for an item across all columns.
    /// </summary>
    private Dictionary<string, string> PreComputeSearchableValues(TItem item)
    {
        var values = new Dictionary<string, string>();
        Logger.Debug("Pre-computing searchable values for columns: {Columns}",
            string.Join(", ", _columns.Select(c => c.PropertyName)));

        foreach (var column in _columns.Where(c => c.PropertySelector != null))
        {
            if (!_compiledSelectors.TryGetValue(column.PropertyName, out var selector))
            {
                selector = column.PropertySelector!.Compile();
                _compiledSelectors[column.PropertyName] = selector;
                Logger.Debug("Compiled selector for column: {Column}", column.PropertyName);
            }

            var value = selector(item);
            if (value != null)
            {
                var stringValue = (value switch
                {
                    DateTime date => date.ToString(column.Format),
                    DateTimeOffset dtOffset => dtOffset.ToString(column.Format),
                    IFormattable formattable => formattable.ToString(column.Format, null),
                    _ => value.ToString() ?? string.Empty
                }).ToLowerInvariant();

                values[column.PropertyName] = stringValue;
                Logger.Debug("Column {Column} value: {Value}", column.PropertyName, stringValue);
            }
            else
            {
                Logger.Debug("Column {Column} has null value", column.PropertyName);
            }
        }

        Logger.Debug("Completed pre-computing values: {Values}",
            string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value}")));
        return values;
    }

    /// <summary>
    ///     Optimized method to check if an item matches the search terms.
    /// </summary>
    private bool ItemMatchesSearch(TItem item, string[] searchTerms)
    {
        if (!_searchableValues.TryGetValue(item, out var values))
        {
            values = PreComputeSearchableValues(item);
            _searchableValues.Add(item, values);
        }

        // Debug logging to check values
        Logger.Debug("Checking item with values: {Values}", string.Join(", ", values.Values));
        Logger.Debug("Against search terms: {Terms}", string.Join(", ", searchTerms));

        // Check if ALL search terms match ANY column value
        return searchTerms.All(term =>
            values.Values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase))
        );
    }

    /// <summary>
    ///     Performs the actual search operation with optimized performance.
    /// </summary>
    private IEnumerable<TItem> PerformSearch(IEnumerable<TItem> items, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return items;
        }

        // Split search term and ensure we have valid terms
        var searchTerms = searchTerm.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!searchTerms.Any())
        {
            return items;
        }

        _searchTermCache[searchTerm] = searchTerms;

        var itemsList = items.ToList();
        Logger.Debug("Starting search with {Count} items for terms: {Terms}",
            itemsList.Count, string.Join(", ", searchTerms));

        IEnumerable<TItem> results;
        if (itemsList.Count > _parallelThreshold)
        {
            results = itemsList.AsParallel()
                .Where(item => ItemMatchesSearch(item, searchTerms))
                .AsEnumerable();
        }
        else
        {
            results = itemsList.Where(item => ItemMatchesSearch(item, searchTerms));
        }

        var resultList = results.ToList();
        Logger.Debug("Search completed. Found {Count} matching items", resultList.Count);
        return resultList;
    }

    /// <summary>
    ///     Performs the search logic after the debounce delay with performance optimization.
    /// </summary>
    private async Task DebounceSearchAsync()
    {
        try
        {
            _isSearching = true;
            await InvokeAsync(StateHasChanged);

            Logger.Debug("Performing search with term: {SearchTerm}", SearchTerm);
            _metricsService.StartSearchTimer();

            var totalItems = Items?.Count() ?? 0;

            FilteredItems = Items is not null ? PerformSearch(Items, SearchTerm).ToList() : new List<TItem>();

            CurrentPage = 1;
            UpdateDisplayedItems();

            _metricsService.StopSearchTimer(
                totalItems,
                FilteredItems.Count(),
                DisplayedItems.Count());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during search in DropBearDataGrid.");
            _metricsService.Reset();
        }
        finally
        {
            _isSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void UpdateDisplayedItems()
    {
        var items = FilteredItems;

        if (_currentSortColumn?.PropertySelector != null)
        {
            var selector = _currentSortColumn.PropertySelector.Compile();
            items = _currentSortDirection == SortDirection.Ascending
                ? items.OrderBy(selector)
                : items.OrderByDescending(selector);
        }

        DisplayedItems = items
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        Logger.Debug("Displayed items updated. Page={CurrentPage}, ItemsPerPage={ItemsPerPage}", CurrentPage,
            ItemsPerPage);
    }

    private void SortBy(DataGridColumn<TItem> column)
    {
        if (!column.Sortable)
        {
            return;
        }

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

        Logger.Debug("Sorting by column: {Column}, Direction: {Direction}", column.Title, _currentSortDirection);
        UpdateDisplayedItems();
    }

    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
    }

    private static string GetSortIconClass(SortDirection? sortDirection)
    {
        return sortDirection switch
        {
            SortDirection.Ascending => "fa-sort-up",
            SortDirection.Descending => "fa-sort-down",
            _ => "fa-sort"
        };
    }

    private void PreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        Logger.Debug("Moved to previous page: {CurrentPage}", CurrentPage);
        UpdateDisplayedItems();
    }

    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        Logger.Debug("Moved to next page: {CurrentPage}", CurrentPage);
        UpdateDisplayedItems();
    }

    private void ItemsPerPageChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var itemsPerPage))
        {
            ItemsPerPage = itemsPerPage;
            CurrentPage = 1;
            UpdateDisplayedItems();
            Logger.Debug("ItemsPerPage changed to: {ItemsPerPage}", ItemsPerPage);
        }
    }

    private void ToggleSelection(TItem item, bool isSelected)
    {
        if (isSelected)
        {
            if (!_selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
                Logger.Debug("Item selected: {Item}", item);
            }
        }
        else
        {
            _selectedItems.Remove(item);
            Logger.Debug("Item deselected: {Item}", item);
        }

        _ = OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
        StateHasChanged();
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedItems.Clear();
        if (selectAll && FilteredItems.Any())
        {
            _selectedItems.AddRange(FilteredItems);
            Logger.Debug("All items selected.");
        }
        else
        {
            Logger.Debug("All items deselected.");
        }

        _ = OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
        StateHasChanged();
    }

    private async Task ExportDataAsync()
    {
        Logger.Debug("Export triggered.");
        if (OnExportData.HasDelegate)
        {
            await OnExportData.InvokeAsync();
        }
    }

    private async Task AddItemAsync()
    {
        Logger.Debug("Add item triggered.");
        if (OnAddItem.HasDelegate)
        {
            await OnAddItem.InvokeAsync();
        }
    }

    private async Task EditItemAsync(TItem item)
    {
        Logger.Debug("Edit item triggered for: {Item}", item);
        if (OnEditItem.HasDelegate)
        {
            await OnEditItem.InvokeAsync(item);
        }
    }

    private async Task DeleteItemAsync(TItem item)
    {
        Logger.Debug("Delete item triggered for: {Item}", item);
        if (OnDeleteItem.HasDelegate)
        {
            await OnDeleteItem.InvokeAsync(item);
        }
    }

    /// <summary>
    ///     Adds a column to the grid with optimized property selector compilation.
    /// </summary>
    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (_columns.Any(c => c.PropertyName == column.PropertyName))
        {
            Logger.Warning("Column with property name {PropertyName} already exists, not added.", column.PropertyName);
            return;
        }

        // Pre-compile the property selector if available
        if (column.PropertySelector != null && !_compiledSelectors.ContainsKey(column.PropertyName))
        {
            _compiledSelectors[column.PropertyName] = column.PropertySelector.Compile();
        }

        _columns.Add(column);
        StateHasChanged();
    }

    /// <summary>
    ///     Resets all columns and clears associated caches.
    /// </summary>
    public void ResetColumns()
    {
        _columns.Clear();
        _compiledSelectors.Clear();
        _searchableValues.Clear();
        _searchTermCache.Clear();
        Logger.Debug("Columns and caches reset in DropBearDataGrid.");
        StateHasChanged();
    }

    /// <summary>
    ///     Gets the formatted value for a cell with optimized property access.
    /// </summary>
    private string GetFormattedValue(TItem item, DataGridColumn<TItem> column)
    {
        if (!_compiledSelectors.TryGetValue(column.PropertyName, out var selector))
        {
            if (column.PropertySelector == null)
            {
                return string.Empty;
            }

            selector = column.PropertySelector.Compile();
            _compiledSelectors[column.PropertyName] = selector;
        }

        var value = selector(item);
        return value switch
        {
            IFormattable formattable => formattable.ToString(column.Format, null),
            _ => value.ToString() ?? string.Empty
        };
    }

    private bool IsItemSelected(TItem item)
    {
        return _selectedItems.Contains(item);
    }

    private async Task HandleRowClickAsync(TItem item)
    {
        Logger.Debug("Row clicked for item: {Item}", item);
        if (OnRowClicked.HasDelegate)
        {
            await OnRowClicked.InvokeAsync(item);
        }
    }

    private async Task HandleRowDoubleClickAsync(TItem item)
    {
        Logger.Debug("Row double-clicked for item: {Item}", item);
        if (OnRowDoubleClicked.HasDelegate)
        {
            await OnRowDoubleClicked.InvokeAsync(item);
        }
    }

    private async Task HandleRowContextMenuAsync(MouseEventArgs e, TItem item)
    {
        await HandleRowClickAsync(item);
        // Context menu is handled by the DropBearContextMenu component if present
    }
}
