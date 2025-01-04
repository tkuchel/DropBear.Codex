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
    private const int DebounceDelay = 300; // milliseconds
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();

    private readonly List<DataGridColumn<TItem>> _columns = new();
    private readonly List<TItem> _selectedItems = new();

    private DataGridColumn<TItem>? _currentSortColumn;
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private Timer? _debounceTimer;
    private bool _isInitialized;
    private bool _isSearching;

    // We track this to detect when Items has changed.
    private IEnumerable<TItem>? _previousItems = Enumerable.Empty<TItem>();
    private ElementReference _searchInput;

    [Parameter] public IEnumerable<TItem>? Items { get; set; } = Enumerable.Empty<TItem>();

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

    [Parameter] public RenderFragment Columns { get; set; } = default!;

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
        new ReadOnlyCollection<int>(new[] { 10, 25, 50, 100 });

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
    ///     Dispose of any unmanaged resources (timers, etc.).
    /// </summary>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // Reload data if the Items reference has changed.
        if (!ReferenceEquals(_previousItems, Items))
        {
            _previousItems = Items;
            await LoadDataAsync();
        }

        _isInitialized = true;
    }

    /// <summary>
    ///     Handles the data loading process (simulated with a small delay).
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            // Simulate data loading delay if necessary.
            await Task.Delay(50);

            FilteredItems = Items?.ToList() ?? new List<TItem>();
            UpdateDisplayedItems();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while loading data.");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Called whenever the user types in the search box. Debounces the search.
    /// </summary>
    private void OnSearchInput(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ => await DebounceSearchAsync(), null, DebounceDelay, Timeout.Infinite);
    }

    /// <summary>
    ///     Runs the search logic after the debounce period.
    /// </summary>
    private async Task DebounceSearchAsync()
    {
        try
        {
            _isSearching = true;
            await InvokeAsync(StateHasChanged);

            Logger.Debug("Performing search with term: {SearchTerm}", SearchTerm);
            await Task.Delay(50); // Simulate search delay

            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                FilteredItems = Items?.ToList() ?? new List<TItem>();
            }
            else
            {
                var searchLower = SearchTerm.ToLowerInvariant();
                if (Items is not null)
                {
                    // Evaluate search against all columns where a PropertySelector is defined.
                    FilteredItems = Items
                        .Where(item => _columns.Any(column =>
                            MatchesSearchTerm(column.PropertySelector, item, searchLower, column.Format)))
                        .ToList();
                }
            }

            CurrentPage = 1;
            UpdateDisplayedItems();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred during search.");
        }
        finally
        {
            _isSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    ///     Re-applies sorting and pagination to update the items displayed on the current page.
    /// </summary>
    private void UpdateDisplayedItems()
    {
        var items = FilteredItems;

        if (_currentSortColumn?.PropertySelector is not null)
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

        Logger.Debug("Updated displayed items. Current page: {CurrentPage}, Items per page: {ItemsPerPage}",
            CurrentPage, ItemsPerPage);
    }

    /// <summary>
    ///     Checks whether a given item matches a search term based on a column's property selector and format.
    /// </summary>
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
            DateTime dateTime => dateTime.ToString(format).ToLowerInvariant(),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(format).ToLowerInvariant(),
            _ => value.ToString()?.ToLowerInvariant() ?? string.Empty
        };

        return valueString.Contains(searchTerm);
    }

    /// <summary>
    ///     Invoked when the user clicks on a column header to sort by that column.
    /// </summary>
    private void SortBy(DataGridColumn<TItem> column)
    {
        if (!column.Sortable)
        {
            return;
        }

        if (_currentSortColumn == column)
        {
            // Toggle sort direction
            _currentSortDirection = _currentSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _currentSortDirection = SortDirection.Ascending;
            _currentSortColumn = column;
        }

        Logger.Debug("Sorting by column: {Column}, Direction: {Direction}", column.Title, _currentSortDirection);
        UpdateDisplayedItems();
    }

    /// <summary>
    ///     Returns the current sort direction for the specified column, or null if it is not being sorted.
    /// </summary>
    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
    }

    /// <summary>
    ///     Determines which icon class to display based on the current sort direction.
    /// </summary>
    private static string GetSortIconClass(SortDirection? sortDirection)
    {
        return sortDirection switch
        {
            SortDirection.Ascending => "fa-sort-up",
            SortDirection.Descending => "fa-sort-down",
            _ => "fa-sort"
        };
    }

    /// <summary>
    ///     Moves to the previous page of items (if not on the first page).
    /// </summary>
    private void PreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        Logger.Debug("Navigated to previous page: {CurrentPage}", CurrentPage);
        UpdateDisplayedItems();
    }

    /// <summary>
    ///     Moves to the next page of items (if not on the last page).
    /// </summary>
    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        Logger.Debug("Navigated to next page: {CurrentPage}", CurrentPage);
        UpdateDisplayedItems();
    }

    /// <summary>
    ///     Handles changes to the 'ItemsPerPage' dropdown.
    /// </summary>
    private void ItemsPerPageChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var itemsPerPage))
        {
            ItemsPerPage = itemsPerPage;
            CurrentPage = 1;
            UpdateDisplayedItems();

            Logger.Debug("Items per page changed to: {ItemsPerPage}", ItemsPerPage);
        }
    }

    /// <summary>
    ///     Toggles selection of an individual item.
    /// </summary>
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

    /// <summary>
    ///     Toggles selection of all items in the filtered set (i.e., select all or deselect all).
    /// </summary>
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

    /// <summary>
    ///     Triggers the export action if provided via OnExportData callback.
    /// </summary>
    private async Task ExportDataAsync()
    {
        Logger.Debug("Export data triggered.");
        if (OnExportData.HasDelegate)
        {
            await OnExportData.InvokeAsync();
        }
    }

    /// <summary>
    ///     Triggers the add-item action if provided via OnAddItem callback.
    /// </summary>
    private async Task AddItemAsync()
    {
        Logger.Debug("Add item triggered.");
        if (OnAddItem.HasDelegate)
        {
            await OnAddItem.InvokeAsync();
        }
    }

    /// <summary>
    ///     Triggers the edit action for a specific item if provided via OnEditItem callback.
    /// </summary>
    private async Task EditItemAsync(TItem item)
    {
        Logger.Debug("Edit item triggered for item: {Item}", item);
        if (OnEditItem.HasDelegate)
        {
            await OnEditItem.InvokeAsync(item);
        }
    }

    /// <summary>
    ///     Triggers the delete action for a specific item if provided via OnDeleteItem callback.
    /// </summary>
    private async Task DeleteItemAsync(TItem item)
    {
        Logger.Debug("Delete item triggered for item: {Item}", item);
        if (OnDeleteItem.HasDelegate)
        {
            await OnDeleteItem.InvokeAsync(item);
        }
    }

    /// <summary>
    ///     Adds a new column to the grid if not already present.
    /// </summary>
    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (_columns.Any(c => c.PropertyName == column.PropertyName))
        {
            Logger.Warning("Column with the property name {PropertyName} already exists and will not be added.",
                column.PropertyName);
            return;
        }

        _columns.Add(column);
        StateHasChanged();
    }

    /// <summary>
    ///     Clears all columns from the grid.
    /// </summary>
    public void ResetColumns()
    {
        _columns.Clear();
        Logger.Debug("Columns reset.");
        StateHasChanged();
    }

    /// <summary>
    ///     Returns the string representation of a column's value for a given item,
    ///     formatted if the value is IFormattable.
    /// </summary>
    private string GetFormattedValue(TItem item, DataGridColumn<TItem> column)
    {
        var value = column.PropertySelector?.Compile()(item);
        return value switch
        {
            IFormattable formattable => formattable.ToString(column.Format, null),
            _ => value?.ToString() ?? string.Empty
        };
    }

    /// <summary>
    ///     Checks if an item is in the currently selected items list.
    /// </summary>
    private bool IsItemSelected(TItem item)
    {
        return _selectedItems.Contains(item);
    }

    /// <summary>
    ///     Handles a row click event.
    /// </summary>
    private async Task HandleRowClickAsync(TItem item)
    {
        Logger.Debug("Row clicked for item: {Item}", item);
        if (OnRowClicked.HasDelegate)
        {
            await OnRowClicked.InvokeAsync(item);
        }
    }

    /// <summary>
    ///     Handles a row double-click event.
    /// </summary>
    private async Task HandleRowDoubleClickAsync(TItem item)
    {
        Logger.Debug("Row double-clicked for item: {Item}", item);
        if (OnRowDoubleClicked.HasDelegate)
        {
            await OnRowDoubleClicked.InvokeAsync(item);
        }
    }

    /// <summary>
    ///     Handles a right-click event on a row, also preventing the default context menu.
    /// </summary>
    private async Task HandleRowContextMenuAsync(MouseEventArgs e, TItem item)
    {
        await HandleRowClickAsync(item);
        // The DropBearContextMenu component can handle its own context menu display.
    }
}
