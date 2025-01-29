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
    private const int DebounceDelay = 300;
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    // Use array instead of ReadOnlyCollection for better performance
    private static readonly int[] ItemsPerPageOptions = [10, 25, 50, 100];

    // Use readonly collections for thread safety
    private readonly List<DataGridColumn<TItem>> _columns = new(50); // Preallocate capacity
    private readonly Dictionary<string, Func<TItem, object>> _compiledSelectors = new(50);
    private readonly DataGridMetricsService _metricsService = new();
    private const int ParallelThreshold = 500;
    private readonly ConditionalWeakTable<TItem, Dictionary<string, string>> _searchableValues = new();
    private readonly ConcurrentDictionary<string, string[]> _searchTermCache = new();
    private readonly HashSet<TItem> _selectedItems = []; // HashSet for O(1) lookups
    private List<TItem>? _cachedFilteredItems;

    // Cache frequently accessed values
    private List<TItem>? _cachedItems;

    private DataGridColumn<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private CancellationTokenSource? _debounceTokenSource;
    private bool _isInitialized;
    private bool _isSearching;
    private IEnumerable<TItem>? _previousItems;
    private string _previousSearchTerm = string.Empty;
    private ElementReference _searchInput;

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
    [Parameter] public RenderFragment Columns { get; set; } = null!;
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }

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
    public override async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }
    private async ValueTask DisposeAsyncCore()
    {
        if (_debounceTokenSource != null)
        {
            await _debounceTokenSource.CancelAsync();
            _debounceTokenSource.Dispose();
        }

        _searchableValues.Clear();
        _searchTermCache.Clear();
        _compiledSelectors.Clear();
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

            if (_cachedItems?.Count > ParallelThreshold)
            {
                await PreComputeSearchableValuesParallel();
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
    private async void OnSearchInput(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;

        // Cancel previous debounce if any
        if (_debounceTokenSource != null)
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
            // Ignore cancellation
        }
    }
    private Dictionary<string, string> PreComputeSearchableValues(TItem item)
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
            DateTimeOffset dtOffset => dtOffset.ToString(format),
            IFormattable formattable => formattable.ToString(format, null),
            _ => value.ToString() ?? string.Empty
        };
    }
    private void PreComputeSearchableValuesSequential()
    {
        if (_cachedItems == null)
        {
            return;
        }

        foreach (var item in _cachedItems)
        {
            if (!_searchableValues.TryGetValue(item, out _))
            {
                _searchableValues.Add(item, PreComputeSearchableValues(item));
            }
        }
    }
    private Task PreComputeSearchableValuesParallel()
    {
        if (_cachedItems == null)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            Parallel.ForEach(_cachedItems, item =>
            {
                if (!_searchableValues.TryGetValue(item, out _))
                {
                    _searchableValues.Add(item, PreComputeSearchableValues(item));
                }
            });
        });
    }
    private bool ItemMatchesSearch(TItem item, string[] searchTerms)
    {
        if (!_searchableValues.TryGetValue(item, out var values))
        {
            values = PreComputeSearchableValues(item);
            _searchableValues.Add(item, values);
        }

        // Use span for better performance with string operations
        ReadOnlySpan<string> terms = searchTerms;
        foreach (var term in terms)
        {
            var found = false;
            foreach (var value in values.Values)
            {
                if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
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

        // Cache search terms to avoid repeated string operations
        if (!_searchTermCache.TryGetValue(searchTerm, out var searchTerms))
        {
            searchTerms = searchTerm.Split([' ', ',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(term => term.ToLowerInvariant())
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .ToArray();

            _searchTermCache.TryAdd(searchTerm, searchTerms);
        }

        if (searchTerms.Length == 0)
        {
            return items;
        }

        var itemsList = items as IList<TItem> ?? items?.ToList();
        if (itemsList == null)
        {
            return [];
        }

        if (itemsList.Count > ParallelThreshold)
        {
            return itemsList.AsParallel()
                .Where(item => ItemMatchesSearch(item, searchTerms))
                .ToList();
        }

        return itemsList.Where(item => ItemMatchesSearch(item, searchTerms)).ToList();
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
                : new List<TItem>();

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
    private void UpdateDisplayedItems()
    {
        if (_cachedFilteredItems == null)
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
    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (column == null)
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
            _compiledSelectors.TryAdd(
                column.PropertyName,
                column.PropertySelector.Compile());
        }

        _columns.Add(column);
        _searchableValues.Clear();

        if (_cachedItems != null)
        {
            if (_cachedItems.Count > ParallelThreshold)
            {
                _ = PreComputeSearchableValuesParallel();
            }
            else
            {
                PreComputeSearchableValuesSequential();
            }
        }

        StateHasChanged();
    }
    public void ResetColumns()
    {
        _columns.Clear();
        _compiledSelectors.Clear();
        _searchableValues.Clear();
        _searchTermCache.Clear();
        StateHasChanged();
    }
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

    // Event handlers with minimal allocations
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

    // Navigation methods with bounds checking
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

        _currentSortDirection = _currentSortColumn == column
            ? _currentSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending
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
    private bool IsItemSelected(TItem item)
    {
        return _selectedItems.Contains(item);
    }
    private async void ToggleSelection(TItem item, bool isSelected)
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
        if (selectAll && FilteredItems.Any())
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
}
