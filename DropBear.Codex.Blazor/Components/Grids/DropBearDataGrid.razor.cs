#region

using System.Collections.ObjectModel;
using System.Linq.Expressions;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
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
public sealed partial class DropBearDataGrid<TItem> : DropBearComponentBase, IDisposable
{
    private const int DebounceDelay = 300; // ms
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    private readonly List<DataGridColumn<TItem>> _columns = new();
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

    /// <summary>
    ///     The current text value for searching/filtering the grid.
    /// </summary>
    private string SearchTerm { get; set; } = string.Empty;

    private int CurrentPage { get; set; } = 1;

    private IEnumerable<TItem> FilteredItems { get; set; } = new List<TItem>();
    private IEnumerable<TItem> DisplayedItems { get; set; } = new List<TItem>();

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

    /// <inheritdoc />
    public void Dispose()
    {
        _debounceTimer?.Dispose();
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
    ///     Handles the data loading process (with optional delay).
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            await Task.Delay(50); // Optional data loading delay

            FilteredItems = Items?.ToList() ?? new List<TItem>();
            UpdateDisplayedItems();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading data in DropBearDataGrid.");
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
    ///     Performs the search logic after the debounce delay.
    /// </summary>
    private async Task DebounceSearchAsync()
    {
        try
        {
            _isSearching = true;
            await InvokeAsync(StateHasChanged);

            Logger.Debug("Performing search with term: {SearchTerm}", SearchTerm);
            await Task.Delay(50); // Simulate a short search delay

            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                FilteredItems = Items?.ToList() ?? new List<TItem>();
            }
            else if (Items is not null)
            {
                var lowerTerm = SearchTerm.ToLowerInvariant();
                FilteredItems = Items.Where(item =>
                    _columns.Any(column => MatchesSearchTerm(column.PropertySelector, item, lowerTerm, column.Format))
                ).ToList();
            }

            CurrentPage = 1;
            UpdateDisplayedItems();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during search in DropBearDataGrid.");
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

    private static bool MatchesSearchTerm(
        Expression<Func<TItem, object>>? selector,
        TItem item,
        string searchTerm,
        string format)
    {
        if (selector is null)
        {
            return false;
        }

        var compiledSelector = selector.Compile();
        var value = compiledSelector(item);
        if (value is null)
        {
            return false;
        }

        var valueString = value switch
        {
            DateTime date => date.ToString(format).ToLowerInvariant(),
            DateTimeOffset dtOffset => dtOffset.ToString(format).ToLowerInvariant(),
            _ => value.ToString()?.ToLowerInvariant() ?? string.Empty
        };

        return valueString.Contains(searchTerm);
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

    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (_columns.Any(c => c.PropertyName == column.PropertyName))
        {
            Logger.Warning("Column with property name {PropertyName} already exists, not added.", column.PropertyName);
            return;
        }

        _columns.Add(column);
        StateHasChanged();
    }

    public void ResetColumns()
    {
        _columns.Clear();
        Logger.Debug("Columns reset in DropBearDataGrid.");
        StateHasChanged();
    }

    private string GetFormattedValue(TItem item, DataGridColumn<TItem> column)
    {
        var value = column.PropertySelector?.Compile()(item);
        return value switch
        {
            IFormattable formattable => formattable.ToString(column.Format, null),
            _ => value?.ToString() ?? string.Empty
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
