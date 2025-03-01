﻿#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Extensions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A Blazor component for rendering a data grid with sorting, searching, and pagination capabilities.
///     Optimized for performance with large datasets and resilience against network issues.
/// </summary>
/// <typeparam name="TItem">The type of the data items.</typeparam>
public sealed partial class DropBearDataGrid<TItem> : DropBearComponentBase where TItem : class
{
    #region Fields and Constants

    private const int DebounceDelay = 300;
    private const int ParallelThreshold = 500;
    private const int SearchCacheMaxSize = 1000;
    private const int SelectorCacheMaxSize = 100;
    private const int CircuitReconnectTimeoutMs = 30000;
    private const int ErrorDisplayTimeMs = 5000;

    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    // Use a strongly typed array for performance.
    private static readonly int[] ItemsPerPageOptions = [10, 25, 50, 100];

    // Collections for columns, selectors, and search caching.
    private readonly List<DataGridColumn<TItem>> _columns = new(50);

    private readonly MemoryCache _compiledSelectors = new(new MemoryCacheOptions
    {
        SizeLimit = SelectorCacheMaxSize, ExpirationScanFrequency = TimeSpan.FromMinutes(10)
    });

    private readonly ConditionalWeakTable<TItem, Dictionary<string, string>> _searchableValues = new();

    private readonly MemoryCache _searchTermCache = new(new MemoryCacheOptions
    {
        SizeLimit = SearchCacheMaxSize, ExpirationScanFrequency = TimeSpan.FromMinutes(5)
    });

    private readonly HashSet<TItem> _selectedItems = new();
    private readonly DataGridMetricsService _metricsService = new();

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
    private Result<bool, DataGridError>? _lastOperationResult;
    private DateTime _errorDisplayUntil;

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
    [Parameter] public bool AllowDownload { get; set; }
    [Parameter] public bool AllowExport { get; set; }
    [Parameter] public bool EnableDebugMode { get; set; }
    [Parameter] public bool ShowErrorNotifications { get; set; } = true;
    [Parameter] public EventCallback OnExportData { get; set; }
    [Parameter] public EventCallback OnAddItem { get; set; }
    [Parameter] public EventCallback<TItem> OnEditItem { get; set; }
    [Parameter] public EventCallback<TItem> OnDeleteItem { get; set; }
    [Parameter] public EventCallback<TItem> OnDownloadItem { get; set; }
    [Parameter] public EventCallback<List<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClicked { get; set; }
    [Parameter] public EventCallback<TItem> OnRowDoubleClicked { get; set; }
    [Parameter] public EventCallback<Result<bool, DataGridError>> OnOperationError { get; set; }
    [Parameter] public RenderFragment Columns { get; set; } = default!;
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }
    [Parameter] public RenderFragment<DataGridError>? ErrorTemplate { get; set; }

    #endregion

    #region Private Properties

    private string SearchTerm { get; set; } = string.Empty;
    private int CurrentPage { get; set; } = 1;
    private IEnumerable<TItem> FilteredItems => _cachedFilteredItems ?? Enumerable.Empty<TItem>();
    private IEnumerable<TItem> DisplayedItems { get; set; } = Array.Empty<TItem>();
    private bool ShowMetrics => EnableDebugMode && _metricsService.IsEnabled;
    private bool IsLoading { get; set; } = true;
    private bool HasData => FilteredItems.Any();

    private bool ShowError => ShowErrorNotifications &&
                              _lastOperationResult != null &&
                              !_lastOperationResult.IsSuccess &&
                              DateTime.UtcNow < _errorDisplayUntil;

    private int TotalPages => _cachedFilteredItems?.Count > 0
        ? (int)Math.Ceiling(_cachedFilteredItems.Count / (double)ItemsPerPage)
        : 0;

    private int TotalColumnCount => _columns.Count + (EnableMultiSelect ? 1 : 0) + (AllowEdit || AllowDelete ? 1 : 0);

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets a read-only list of the current columns.
    /// </summary>
    public IReadOnlyList<DataGridColumn<TItem>> GetColumns => _columns.AsReadOnly();

    /// <summary>
    ///     Gets the total number of items in the grid.
    /// </summary>
    public int TotalItemCount => _cachedItems?.Count ?? 0;

    /// <summary>
    ///     Gets the number of items that match the current filter.
    /// </summary>
    public int FilteredItemCount => _cachedFilteredItems?.Count ?? 0;

    /// <summary>
    ///     Gets the number of currently selected items.
    /// </summary>
    public int SelectedItemCount => _selectedItems.Count;

    /// <summary>
    ///     Gets a read-only list of the currently selected items.
    /// </summary>
    public IReadOnlyList<TItem> SelectedItems => _selectedItems.ToList().AsReadOnly();

    #endregion

    #region Lifecycle Methods

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (_debounceTokenSource is not null)
            {
                await _debounceTokenSource.CancelAsync();
                _debounceTokenSource.Dispose();
            }

            _searchableValues.Clear();

            // Clear memory caches
            _searchTermCache.Dispose();
            _compiledSelectors.Dispose();
            _updateLock.Dispose();

            _compiledSelectors.Clear();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during DropBearDataGrid disposal");
        }

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
    ///     Uses the Result pattern for error handling.
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            await _updateLock.WaitAsync();

            try
            {
                IsLoading = true;
                _metricsService.IsEnabled = EnableDebugMode;
                _metricsService.StartSearchTimer();
                StateHasChanged();

                // Use a fresh weak table to avoid memory issues
                var newSearchableValues = new ConditionalWeakTable<TItem, Dictionary<string, string>>();

                if (_cachedItems is not null)
                {
                    if (_cachedItems.Count > ParallelThreshold)
                    {
                        await PreComputeSearchableValuesParallelAsync(newSearchableValues);
                    }
                    else
                    {
                        PreComputeSearchableValuesSequential(newSearchableValues);
                    }

                    // Replace the old table with the new one
                    _searchableValues.Clear();
                    foreach (var item in _cachedItems)
                    {
                        if (newSearchableValues.TryGetValue(item, out var values))
                        {
                            _searchableValues.Add(item, values);
                        }
                    }
                }

                _cachedFilteredItems = _cachedItems;
                UpdateDisplayedItems();

                _metricsService.StopSearchTimer(
                    _cachedItems?.Count ?? 0,
                    _cachedFilteredItems?.Count ?? 0,
                    DisplayedItems.Count());

                _lastOperationResult = Result<bool, DataGridError>.Success(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading data in DropBearDataGrid.");
                _metricsService.Reset();

                _lastOperationResult = Result<bool, DataGridError>.Failure(
                    DataGridError.LoadingFailed(ex.Message), ex);

                _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);

                if (OnOperationError.HasDelegate)
                {
                    await OnOperationError.InvokeAsync(_lastOperationResult);
                }
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Reloads data from scratch, clearing all caches.
    /// </summary>
    public async Task ReloadDataAsync()
    {
        _searchableValues.Clear();
        _searchTermCache.Clear();
        _cachedFilteredItems = null;
        _cachedItems = Items?.ToList();
        await LoadDataAsync();
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

        foreach (var column in _columns.Where(c => c.PropertySelector != null && c.Filterable))
        {
            try
            {
                var selectorKey = $"{typeof(TItem).Name}.{column.PropertyName}";
                if (!_compiledSelectors.TryGetValue(selectorKey, out Func<TItem, object>? selector))
                {
                    if (column.PropertySelector == null)
                    {
                        continue;
                    }

                    selector = column.PropertySelector.Compile();
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30)
                    };
                    _compiledSelectors.Set(selectorKey, selector, cacheOptions);
                }

                var rawValue = selector!(item);
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
            DateTime date => string.IsNullOrEmpty(format) ? date.ToString() : date.ToString(format),
            DateTimeOffset dto => string.IsNullOrEmpty(format) ? dto.ToString() : dto.ToString(format),
            IFormattable formattable => formattable.ToString(format, null),
            _ => value.ToString() ?? string.Empty
        };
    }

    private void PreComputeSearchableValuesSequential(
        ConditionalWeakTable<TItem, Dictionary<string, string>> searchValues)
    {
        if (_cachedItems is null)
        {
            return;
        }

        foreach (var item in _cachedItems)
        {
            if (!searchValues.TryGetValue(item, out _))
            {
                searchValues.Add(item, ComputeSearchableValues(item));
            }
        }
    }

    private Task PreComputeSearchableValuesParallelAsync(
        ConditionalWeakTable<TItem, Dictionary<string, string>> searchValues)
    {
        if (_cachedItems is null)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            var localItems = _cachedItems.ToArray(); // Create local copy for thread safety
            var itemsProcessed = 0;

            Parallel.ForEach(localItems, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                item =>
                {
                    try
                    {
                        if (!searchValues.TryGetValue(item, out _))
                        {
                            var values = ComputeSearchableValues(item);
                            searchValues.Add(item, values);

                            Interlocked.Increment(ref itemsProcessed);

                            if (EnableDebugMode && itemsProcessed % 1000 == 0)
                            {
                                Logger.Debug("Processed {Count} of {Total} items for search indexing",
                                    itemsProcessed, localItems.Length);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error pre-computing search values for item");
                    }
                });
        });
    }

    /// <summary>
    ///     Returns the search terms (tokens) for a given search string, using caching.
    /// </summary>
    private string[] GetSearchTerms(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<string>();
        }

        if (!_searchTermCache.TryGetValue(searchTerm, out string[]? terms))
        {
            terms = searchTerm.Split([' ', ',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };

            _searchTermCache.Set(searchTerm, terms, cacheOptions);
        }

        return terms!;
    }

    private bool ItemMatchesSearch(TItem item, string[] searchTerms)
    {
        if (searchTerms.Length == 0)
        {
            return true;
        }

        if (!_searchableValues.TryGetValue(item, out var values))
        {
            values = ComputeSearchableValues(item);
            _searchableValues.Add(item, values);
        }

        // If "AND" search logic is desired (all terms must match)
        foreach (var term in searchTerms)
        {
            var termMatches = false;
            foreach (var value in values.Values)
            {
                if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    termMatches = true;
                    break;
                }
            }

            if (!termMatches)
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
        if (itemsList is null || itemsList.Count == 0)
        {
            return Array.Empty<TItem>();
        }

        return itemsList.Count > ParallelThreshold
            ? itemsList.AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Where(item => ItemMatchesSearch(item, searchTerms))
                .ToList()
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
            await _updateLock.WaitAsync();

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

                _lastOperationResult = Result<bool, DataGridError>.Success(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during search in DropBearDataGrid.");
                _metricsService.Reset();

                _lastOperationResult = Result<bool, DataGridError>.Failure(
                    DataGridError.SearchFailed(SearchTerm, ex.Message), ex);

                _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);

                if (OnOperationError.HasDelegate)
                {
                    await OnOperationError.InvokeAsync(_lastOperationResult);
                }
            }
            finally
            {
                _isSearching = false;
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _updateLock.Release();
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
            try
            {
                var selectorKey = $"{typeof(TItem).Name}.{_currentSortColumn.PropertyName}";

                if (!_compiledSelectors.TryGetValue(selectorKey, out Func<TItem, object>? selector))
                {
                    selector = _currentSortColumn.PropertySelector.Compile();
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30)
                    };
                    _compiledSelectors.Set(selectorKey, selector, cacheOptions);
                }

                // Use OrderBy instead of recreating the list if possible
                items = _currentSortDirection == SortDirection.Ascending
                    ? _cachedFilteredItems.OrderBy(selector!)
                    : _cachedFilteredItems.OrderByDescending(selector!);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sorting column {Column}", _currentSortColumn.PropertyName);

                // Fall back to unsorted list
                items = _cachedFilteredItems;

                _lastOperationResult = Result<bool, DataGridError>.Failure(
                    DataGridError.SortingFailed(_currentSortColumn.PropertyName, ex.Message), ex);

                _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            }
        }

        // Apply pagination
        var skip = (CurrentPage - 1) * ItemsPerPage;
        var take = ItemsPerPage;

        // Check that skip doesn't exceed the collection length
        if (items is ICollection<TItem> collection)
        {
            if (skip >= collection.Count)
            {
                skip = Math.Max(0, collection.Count - take);
                CurrentPage = Math.Max(1, (skip / take) + 1);
            }
        }

        DisplayedItems = items.Skip(skip).Take(take).ToList();
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
            var selectorKey = $"{typeof(TItem).Name}.{column.PropertyName}";
            var selector = column.PropertySelector.Compile();
            var cacheOptions = new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30) };
            _compiledSelectors.Set(selectorKey, selector, cacheOptions);
        }

        _columns.Add(column);

        // Clear the search values to force recomputation
        _searchableValues.Clear();

        if (_cachedItems is not null)
        {
            // Use a new ConditionalWeakTable to avoid threading issues
            var newSearchableValues = new ConditionalWeakTable<TItem, Dictionary<string, string>>();

            if (_cachedItems.Count > ParallelThreshold)
            {
                _ = PreComputeSearchableValuesParallelAsync(newSearchableValues);
            }
            else
            {
                PreComputeSearchableValuesSequential(newSearchableValues);
            }

            // Replace the old table with the new one
            _searchableValues.Clear();
            foreach (var item in _cachedItems)
            {
                if (newSearchableValues.TryGetValue(item, out var values))
                {
                    _searchableValues.Add(item, values);
                }
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
        try
        {
            var selectorKey = $"{typeof(TItem).Name}.{column.PropertyName}";

            if (!_compiledSelectors.TryGetValue(selectorKey, out Func<TItem, object>? selector))
            {
                if (column.PropertySelector == null)
                {
                    return string.Empty;
                }

                selector = column.PropertySelector.Compile();
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30)
                };
                _compiledSelectors.Set(selectorKey, selector, cacheOptions);
            }

            if (selector == null)
            {
                return string.Empty;
            }

            var value = selector(item);
            return FormatValue(value, column.Format);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting formatted value for column {Column}", column.PropertyName);
            return "Error";
        }
    }

    #endregion

    #region Selection and Row Events

    /// <summary>
    ///     Handles changes in row selection, notifying subscribers via the OnSelectionChanged event.
    /// </summary>
    /// <param name="item">The item being selected or deselected.</param>
    /// <param name="isSelected">Whether the item is now selected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleSelectionChangeAsync(TItem item, bool isSelected)
    {
        try
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
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling selection change for item");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to change selection: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Selects or deselects all items in the current filtered set.
    /// </summary>
    /// <param name="selectAll">Whether to select or deselect all items.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleSelectAllAsync(bool selectAll)
    {
        try
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
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling select all operation");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to select/deselect all items: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the export data action, invoking the OnExportData callback.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExportDataAsync()
    {
        try
        {
            if (!OnExportData.HasDelegate)
            {
                Logger.Warning("Export attempted but no handler is registered");
                return;
            }

            // Use circuit connection resilience
            await this.WithCircuitResilienceAsync(
                async token =>
                {
                    await OnExportData.InvokeAsync();
                    return true;
                },
                TimeSpan.FromMilliseconds(CircuitReconnectTimeoutMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting data");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                DataGridError.ExportFailed(ex.Message), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);

            if (OnOperationError.HasDelegate)
            {
                await OnOperationError.InvokeAsync(_lastOperationResult);
            }

            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the add item action, invoking the OnAddItem callback.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AddItemAsync()
    {
        try
        {
            if (!OnAddItem.HasDelegate)
            {
                Logger.Warning("Add attempted but no handler is registered");
                return;
            }

            await this.WithCircuitResilienceAsync(
                async token =>
                {
                    await OnAddItem.InvokeAsync();
                    return true;
                },
                TimeSpan.FromMilliseconds(CircuitReconnectTimeoutMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding item");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to add item: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the edit item action, invoking the OnEditItem callback.
    /// </summary>
    /// <param name="item">The item to edit.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task EditItemAsync(TItem item)
    {
        try
        {
            if (!OnEditItem.HasDelegate)
            {
                Logger.Warning("Edit attempted but no handler is registered");
                return;
            }

            await this.WithCircuitResilienceAsync(
                async token =>
                {
                    await OnEditItem.InvokeAsync(item);
                    return true;
                },
                TimeSpan.FromMilliseconds(CircuitReconnectTimeoutMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error editing item");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to edit item: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the delete item action, invoking the OnDeleteItem callback.
    /// </summary>
    /// <param name="item">The item to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DeleteItemAsync(TItem item)
    {
        try
        {
            if (!OnDeleteItem.HasDelegate)
            {
                Logger.Warning("Delete attempted but no handler is registered");
                return;
            }

            await this.WithCircuitResilienceAsync(
                async token =>
                {
                    await OnDeleteItem.InvokeAsync(item);
                    return true;
                },
                TimeSpan.FromMilliseconds(CircuitReconnectTimeoutMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting item");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to delete item: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles the download item action, invoking the OnDownloadItem callback.
    /// </summary>
    /// <param name="item">The item to download.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DownloadItemAsync(TItem item)
    {
        try
        {
            if (!OnDownloadItem.HasDelegate)
            {
                Logger.Warning("Download attempted but no handler is registered");
                return;
            }

            await this.WithCircuitResilienceAsync(
                async token =>
                {
                    await OnDownloadItem.InvokeAsync(item);
                    return true;
                },
                TimeSpan.FromMilliseconds(CircuitReconnectTimeoutMs)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error downloading item");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                new DataGridError($"Failed to download item: {ex.Message}"), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Handles clicks on a row, invoking the OnRowClicked callback.
    /// </summary>
    /// <param name="item">The item in the clicked row.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleRowClickAsync(TItem item)
    {
        try
        {
            if (OnRowClicked.HasDelegate)
            {
                await OnRowClicked.InvokeAsync(item);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling row click");
        }
    }

    /// <summary>
    ///     Handles double-clicks on a row, invoking the OnRowDoubleClicked callback.
    /// </summary>
    /// <param name="item">The item in the double-clicked row.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleRowDoubleClickAsync(TItem item)
    {
        try
        {
            if (OnRowDoubleClicked.HasDelegate)
            {
                await OnRowDoubleClicked.InvokeAsync(item);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling row double-click");
        }
    }

    /// <summary>
    ///     Handles right-clicks on a row, passing the event to HandleRowClickAsync.
    /// </summary>
    /// <param name="e">The mouse event arguments.</param>
    /// <param name="item">The item in the right-clicked row.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task HandleRowContextMenuAsync(MouseEventArgs e, TItem item)
    {
        return HandleRowClickAsync(item);
    }

    /// <summary>
    ///     Determines whether an item is currently selected.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True if the item is selected; otherwise, false.</returns>
    private bool IsItemSelected(TItem item)
    {
        return _selectedItems.Contains(item);
    }

    /// <summary>
    ///     Toggles selection of an item and notifies subscribers.
    /// </summary>
    /// <param name="item">The item to toggle.</param>
    /// <param name="isSelected">Whether the item should be selected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ToggleSelection(TItem item, bool isSelected)
    {
        try
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

            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to toggle selection");
        }

        StateHasChanged();
    }

    /// <summary>
    ///     Selects or deselects all items in the current filtered set.
    /// </summary>
    /// <param name="selectAll">Whether to select or deselect all items.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ToggleSelectAll(bool selectAll)
    {
        try
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

            if (OnSelectionChanged.HasDelegate)
            {
                await OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to toggle select all");
        }

        StateHasChanged();
    }

    #endregion

    #region Pagination and Sorting

    /// <summary>
    ///     Navigates to the previous page of items.
    /// </summary>
    private void PreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        UpdateDisplayedItems();
    }

    /// <summary>
    ///     Navigates to the next page of items.
    /// </summary>
    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        UpdateDisplayedItems();
    }

    /// <summary>
    ///     Changes the number of items displayed per page.
    /// </summary>
    /// <param name="e">The change event arguments containing the new value.</param>
    private void ItemsPerPageChanged(ChangeEventArgs e)
    {
        try
        {
            if (int.TryParse(e.Value?.ToString(), out var newItemsPerPage) &&
                ItemsPerPageOptions.Contains(newItemsPerPage))
            {
                ItemsPerPage = newItemsPerPage;
                CurrentPage = 1;
                UpdateDisplayedItems();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error changing items per page");
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                DataGridError.PaginationFailed(ex.Message), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Sorts the data by the specified column.
    /// </summary>
    /// <param name="column">The column to sort by.</param>
    private void SortBy(DataGridColumn<TItem> column)
    {
        try
        {
            if (!column.Sortable)
            {
                return;
            }

            if (column.PropertySelector == null)
            {
                Logger.Warning("Cannot sort by column {Column} - property selector is null", column.PropertyName);
                return;
            }

            // Toggle sort direction if the same column is selected; otherwise, start ascending.
            _currentSortDirection = _currentSortColumn == column
                ? _currentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending
                : SortDirection.Ascending;

            _currentSortColumn = column;
            UpdateDisplayedItems();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error sorting by column {Column}", column.PropertyName);
            _lastOperationResult = Result<bool, DataGridError>.Failure(
                DataGridError.SortingFailed(column.PropertyName, ex.Message), ex);
            _errorDisplayUntil = DateTime.UtcNow.AddMilliseconds(ErrorDisplayTimeMs);
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Gets the current sort direction for a column, if any.
    /// </summary>
    /// <param name="column">The column to check.</param>
    /// <returns>The sort direction, or null if the column is not sorted.</returns>
    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
    }

    /// <summary>
    ///     Gets the CSS class for a sort icon based on the sort direction.
    /// </summary>
    /// <param name="direction">The sort direction.</param>
    /// <returns>The CSS class for the appropriate icon.</returns>
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

    #region Public Methods

    /// <summary>
    ///     Clears all selected items.
    /// </summary>
    public void ClearSelection()
    {
        _selectedItems.Clear();
        StateHasChanged();
    }

    /// <summary>
    ///     Selects specific items from the current dataset.
    /// </summary>
    /// <param name="items">The items to select.</param>
    public void SelectItems(IEnumerable<TItem> items)
    {
        foreach (var item in items)
        {
            _selectedItems.Add(item);
        }

        StateHasChanged();
    }

    /// <summary>
    ///     Gets the first selected item, if any.
    /// </summary>
    /// <returns>The first selected item, or null if no items are selected.</returns>
    public TItem? GetFirstSelectedItem()
    {
        return _selectedItems.FirstOrDefault();
    }

    /// <summary>
    ///     Gets a column by property name.
    /// </summary>
    /// <param name="propertyName">The property name of the column to find.</param>
    /// <returns>The column, or null if not found.</returns>
    public DataGridColumn<TItem>? GetColumn(string propertyName)
    {
        return _columns.FirstOrDefault(c => c.PropertyName == propertyName);
    }

    /// <summary>
    ///     Refreshes the grid data, which may be necessary after external changes.
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    #endregion
}
