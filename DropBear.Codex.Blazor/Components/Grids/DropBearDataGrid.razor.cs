#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A Blazor component for rendering a data grid with sorting, searching, and pagination capabilities.
/// </summary>
/// <typeparam name="TItem">The type of the data items.</typeparam>
public sealed partial class DropBearDataGrid<TItem> : DropBearComponentBase where TItem : class
{
    #region Fields and Constants

    private const int DebounceDelay = 300;
    private const int ParallelThreshold = 500;
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    // Use a strongly typed array for performance.
    private static readonly int[] ItemsPerPageOptions = [10, 25, 50, 100];

    // Collections for columns, selectors, and search caching.
    private readonly List<DataGridColumn<TItem>> _columns = new(50);
    private readonly Dictionary<string, Func<TItem, object>> _compiledSelectors = new(50);
    private readonly DataGridMetricsService _metricsService = new();
    private readonly ConditionalWeakTable<TItem, Dictionary<string, string>> _searchableValues = new();
    private readonly ConcurrentDictionary<string, string[]> _searchTermCache = new();
    private readonly HashSet<TItem> _selectedItems = new();

    private List<TItem>? _cachedFilteredItems;
    private List<TItem>? _cachedItems;
    private DataGridColumn<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private CancellationTokenSource? _debounceTokenSource;
    private bool _isInitialized;
    private bool _isSearching;
    private IEnumerable<TItem>? _previousItems;
    private string _previousSearchTerm = string.Empty;
    private ElementReference _searchInput;

    // Semaphore to coordinate UI updates.
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    #endregion

    #region Parameters

    [Parameter] public IEnumerable<TItem>? Items { get; set; }
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
    [Parameter] public RenderFragment Columns { get; set; } = default!;
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }

    #endregion

    #region Private Properties

    private string SearchTerm { get; set; } = string.Empty;
    private int CurrentPage { get; set; } = 1;
    private IEnumerable<TItem> FilteredItems => _cachedFilteredItems ?? Enumerable.Empty<TItem>();
    private IEnumerable<TItem> DisplayedItems { get; set; } = Array.Empty<TItem>();
    private bool ShowMetrics => EnableDebugMode && _metricsService.IsEnabled;
    private bool IsLoading { get; set; } = true;
    private bool HasData => FilteredItems.Any();

    private int TotalPages => _cachedFilteredItems?.Count > 0
        ? (int)Math.Ceiling(_cachedFilteredItems.Count / (double)ItemsPerPage)
        : 0;

    private int TotalColumnCount => _columns.Count + (EnableMultiSelect ? 1 : 0) + (AllowEdit || AllowDelete ? 1 : 0);
    public IReadOnlyList<DataGridColumn<TItem>> GetColumns => _columns.AsReadOnly();

    #endregion

    #region Lifecycle Methods

    public override async ValueTask DisposeAsync()
    {
        if (_debounceTokenSource is not null)
        {
            await _debounceTokenSource.CancelAsync();
            _debounceTokenSource.Dispose();
        }

        _searchableValues.Clear();
        _searchTermCache.Clear();
        _compiledSelectors.Clear();

        await base.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (!ReferenceEquals(_previousItems, Items))
        {
            _previousItems = Items;
            _cachedItems = Items?.ToList();
            await LoadDataAsync();
        }

        _isInitialized = true;
    }

    #endregion

    #region Data Loading and Searching

    /// <summary>
    ///     Loads and precomputes search data, then updates the grid display.
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            _metricsService.IsEnabled = EnableDebugMode;
            _metricsService.StartSearchTimer();
            StateHasChanged();

            _searchableValues.Clear();
            _searchTermCache.Clear();

            if (_cachedItems is not null && _cachedItems.Count > ParallelThreshold)
            {
                await PreComputeSearchableValuesParallelAsync();
            }
            else
            {
                PreComputeSearchableValuesSequential();
            }

            _cachedFilteredItems = _cachedItems;
            UpdateDisplayedItems();

            _metricsService.StopSearchTimer(
                _cachedItems?.Count ?? 0,
                _cachedFilteredItems?.Count ?? 0,
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
    ///     Handles the search input change with debouncing.
    /// </summary>
    private async void OnSearchInput(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;

        // Cancel any existing debounce.
        if (_debounceTokenSource is not null)
        {
            await _debounceTokenSource.CancelAsync();
            _debounceTokenSource.Dispose();
        }

        _debounceTokenSource = new CancellationTokenSource();
        var token = _debounceTokenSource.Token;

        try
        {
            await Task.Delay(DebounceDelay, token);
            if (!token.IsCancellationRequested)
            {
                await DebounceSearchAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation.
        }
    }

    /// <summary>
    ///     Computes searchable values for a given item.
    /// </summary>
    private Dictionary<string, string> ComputeSearchableValues(TItem item)
    {
        var values = new Dictionary<string, string>(_columns.Count);

        foreach (var column in _columns.Where(c => c.PropertySelector != null))
        {
            try
            {
                if (!_compiledSelectors.TryGetValue(column.PropertyName, out var selector))
                {
                    selector = column.PropertySelector!.Compile();
                    _compiledSelectors[column.PropertyName] = selector;
                }

                var rawValue = selector(item);
                if (rawValue != null)
                {
                    values[column.PropertyName] = FormatValue(rawValue, column.Format).ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing column {Column}", column.PropertyName);
            }
        }

        return values;
    }

    private static string FormatValue(object value, string? format)
    {
        return value switch
        {
            DateTime date => date.ToString(format),
            DateTimeOffset dto => dto.ToString(format),
            IFormattable formattable => formattable.ToString(format, null),
            _ => value.ToString() ?? string.Empty
        };
    }

    private void PreComputeSearchableValuesSequential()
    {
        if (_cachedItems is null)
        {
            return;
        }

        foreach (var item in _cachedItems)
        {
            if (!_searchableValues.TryGetValue(item, out _))
            {
                _searchableValues.Add(item, ComputeSearchableValues(item));
            }
        }
    }

    private Task PreComputeSearchableValuesParallelAsync()
    {
        if (_cachedItems is null)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            Parallel.ForEach(_cachedItems, item =>
            {
                if (!_searchableValues.TryGetValue(item, out _))
                {
                    _searchableValues.Add(item, ComputeSearchableValues(item));
                }
            });
        });
    }

    /// <summary>
    ///     Returns the search terms (tokens) for a given search string, using caching.
    /// </summary>
    private string[] GetSearchTerms(string searchTerm)
    {
        if (!_searchTermCache.TryGetValue(searchTerm, out var terms))
        {
            terms = searchTerm.Split([' ', ',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
            _searchTermCache.TryAdd(searchTerm, terms);
        }

        return terms;
    }

    private bool ItemMatchesSearch(TItem item, string[] searchTerms)
    {
        if (!_searchableValues.TryGetValue(item, out var values))
        {
            values = ComputeSearchableValues(item);
            _searchableValues.Add(item, values);
        }

        foreach (var term in searchTerms)
        {
            if (!values.Values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<TItem> PerformSearch(IEnumerable<TItem> items, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return items;
        }

        var searchTerms = GetSearchTerms(searchTerm);
        if (searchTerms.Length == 0)
        {
            return items;
        }

        var itemsList = items as IList<TItem> ?? items.ToList();
        if (itemsList is null)
        {
            return Array.Empty<TItem>();
        }

        return itemsList.Count > ParallelThreshold
            ? itemsList.AsParallel().Where(item => ItemMatchesSearch(item, searchTerms)).ToList()
            : itemsList.Where(item => ItemMatchesSearch(item, searchTerms)).ToList();
    }

    private async Task DebounceSearchAsync()
    {
        if (_previousSearchTerm == SearchTerm)
        {
            return;
        }

        try
        {
            _isSearching = true;
            await InvokeAsync(StateHasChanged);
            _metricsService.StartSearchTimer();

            var totalItems = _cachedItems?.Count ?? 0;
            _previousSearchTerm = SearchTerm;

            _cachedFilteredItems = _cachedItems != null
                ? PerformSearch(_cachedItems, SearchTerm).ToList()
                : [];

            CurrentPage = 1;
            UpdateDisplayedItems();

            _metricsService.StopSearchTimer(
                totalItems,
                _cachedFilteredItems.Count,
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

    /// <summary>
    ///     Updates the subset of items to be displayed based on filtering, sorting, and pagination.
    /// </summary>
    private void UpdateDisplayedItems()
    {
        if (_cachedFilteredItems is null)
        {
            DisplayedItems = Array.Empty<TItem>();
            return;
        }

        IEnumerable<TItem> items = _cachedFilteredItems;

        if (_currentSortColumn?.PropertySelector != null)
        {
            if (!_compiledSelectors.TryGetValue(_currentSortColumn.PropertyName, out var selector))
            {
                selector = _currentSortColumn.PropertySelector.Compile();
                _compiledSelectors[_currentSortColumn.PropertyName] = selector;
            }

            items = _currentSortDirection == SortDirection.Ascending
                ? _cachedFilteredItems.OrderBy(selector)
                : _cachedFilteredItems.OrderByDescending(selector);
        }

        DisplayedItems = items
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();
    }

    #endregion

    #region Column Management

    /// <summary>
    ///     Adds a new column to the grid.
    /// </summary>
    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (column is null)
        {
            throw new ArgumentNullException(nameof(column));
        }

        if (_columns.Any(c => c.PropertyName == column.PropertyName))
        {
            Logger.Warning("Column with property name {PropertyName} already exists.", column.PropertyName);
            return;
        }

        if (column.PropertySelector != null)
        {
            _compiledSelectors.TryAdd(column.PropertyName, column.PropertySelector.Compile());
        }

        _columns.Add(column);
        _searchableValues.Clear();

        if (_cachedItems is not null)
        {
            if (_cachedItems.Count > ParallelThreshold)
            {
                _ = PreComputeSearchableValuesParallelAsync();
            }
            else
            {
                PreComputeSearchableValuesSequential();
            }
        }

        StateHasChanged();
    }

    /// <summary>
    ///     Resets all columns and clears related caches.
    /// </summary>
    public void ResetColumns()
    {
        _columns.Clear();
        _compiledSelectors.Clear();
        _searchableValues.Clear();
        _searchTermCache.Clear();
        StateHasChanged();
    }

    /// <summary>
    ///     Returns the formatted value for a given item and column.
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
        return FormatValue(value, column.Format);
    }

    #endregion

    #region Selection and Row Events

    private async Task HandleSelectionChangeAsync(TItem item, bool isSelected)
    {
        if (isSelected)
        {
            _selectedItems.Add(item);
        }
        else
        {
            _selectedItems.Remove(item);
        }

        if (OnSelectionChanged.HasDelegate)
        {
            await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
        }

        StateHasChanged();
    }

    private async Task HandleSelectAllAsync(bool selectAll)
    {
        _selectedItems.Clear();
        if (selectAll && _cachedFilteredItems?.Count > 0)
        {
            foreach (var item in _cachedFilteredItems)
            {
                _selectedItems.Add(item);
            }
        }

        if (OnSelectionChanged.HasDelegate)
        {
            await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
        }

        StateHasChanged();
    }

    private async Task ExportDataAsync()
    {
        await OnExportData.InvokeAsync();
    }

    private async Task AddItemAsync()
    {
        await OnAddItem.InvokeAsync();
    }

    private async Task EditItemAsync(TItem item)
    {
        await OnEditItem.InvokeAsync(item);
    }

    private async Task DeleteItemAsync(TItem item)
    {
        await OnDeleteItem.InvokeAsync(item);
    }

    private async Task HandleRowClickAsync(TItem item)
    {
        await OnRowClicked.InvokeAsync(item);
    }

    private async Task HandleRowDoubleClickAsync(TItem item)
    {
        await OnRowDoubleClicked.InvokeAsync(item);
    }

    private Task HandleRowContextMenuAsync(MouseEventArgs e, TItem item)
    {
        return HandleRowClickAsync(item);
    }

    private bool IsItemSelected(TItem item)
    {
        return _selectedItems.Contains(item);
    }

    private async Task ToggleSelection(TItem item, bool isSelected)
    {
        if (isSelected)
        {
            if (_selectedItems.Add(item))
            {
                Logger.Debug("Item selected: {Item}", item);
            }
        }
        else
        {
            _selectedItems.Remove(item);
            Logger.Debug("Item deselected: {Item}", item);
        }

        try
        {
            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
            }
            else
            {
                Logger.Warning("OnSelectionChanged delegate is not set.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to invoke OnSelectionChanged.");
        }

        StateHasChanged();
    }

    private async Task ToggleSelectAll(bool selectAll)
    {
        _selectedItems.Clear();
        if (FilteredItems.Any())
        {
            foreach (var item in FilteredItems)
            {
                _selectedItems.Add(item);
            }

            Logger.Debug("All items selected.");
        }
        else
        {
            Logger.Debug("All items deselected.");
        }

        await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
        StateHasChanged();
    }

    #endregion

    #region Pagination and Sorting

    private void PreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        UpdateDisplayedItems();
    }

    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        UpdateDisplayedItems();
    }

    private void ItemsPerPageChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newItemsPerPage) &&
            ItemsPerPageOptions.Contains(newItemsPerPage))
        {
            ItemsPerPage = newItemsPerPage;
            CurrentPage = 1;
            UpdateDisplayedItems();
        }
    }

    private void SortBy(DataGridColumn<TItem> column)
    {
        if (!column.Sortable)
        {
            return;
        }

        // Toggle sort direction if the same column is selected; otherwise, start ascending.
        _currentSortDirection = _currentSortColumn == column
            ? _currentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending
            : SortDirection.Ascending;

        _currentSortColumn = column;
        UpdateDisplayedItems();
    }

    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
    }

    private static string GetSortIconClass(SortDirection? direction)
    {
        return direction switch
        {
            SortDirection.Ascending => "fa-sort-up",
            SortDirection.Descending => "fa-sort-down",
            _ => "fa-sort"
        };
    }

    #endregion
}
