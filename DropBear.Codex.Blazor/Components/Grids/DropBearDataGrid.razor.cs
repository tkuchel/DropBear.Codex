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

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

/// <summary>
///     A Blazor component for rendering a data grid with sorting, searching, and pagination capabilities.
/// </summary>
/// <typeparam name="TItem">The type of the data items.</typeparam>
public partial class DropBearDataGrid<TItem> : DropBearComponentBase, IDisposable
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
    private ElementReference _searchInput;

    [Parameter] public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();

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

    private string SearchTerm { get; set; } = string.Empty;

    private int CurrentPage { get; set; } = 1;

    private int TotalPages => (int)Math.Ceiling(FilteredItems.Count() / (double)ItemsPerPage);

    private IEnumerable<TItem> FilteredItems { get; set; } = new List<TItem>();

    private IEnumerable<TItem> DisplayedItems { get; set; } = new List<TItem>();

    private IReadOnlyCollection<int> ItemsPerPageOptions { get; } =
        new ReadOnlyCollection<int>(new[] { 10, 25, 50, 100 });

    private bool IsLoading { get; set; } = true;

    private bool HasData => FilteredItems.Any();

    private int TotalColumnCount => _columns.Count + (EnableMultiSelect ? 1 : 0) + (AllowEdit || AllowDelete ? 1 : 0);

    public IReadOnlyList<DataGridColumn<TItem>> GetColumns => _columns.AsReadOnly();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadDataAsync();
        _isInitialized = true;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();
            Logger.Debug("Loading data for DropBearDataGrid.");

            // Simulate data loading
            await Task.Delay(200);

            FilteredItems = Items.ToList();
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

    private void OnSearchInput(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ => await DebounceSearchAsync(), null, DebounceDelay, Timeout.Infinite);
    }

    private async Task DebounceSearchAsync()
    {
        try
        {
            _isSearching = true;
            StateHasChanged();

            Logger.Debug("Performing search with term: {SearchTerm}", SearchTerm);

            await Task.Delay(100); // Simulate search delay

            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                FilteredItems = Items.ToList();
            }
            else
            {
                var searchLower = SearchTerm.ToLowerInvariant();
                FilteredItems = Items.Where(item => _columns.Any(column =>
                    MatchesSearchTerm(column.PropertySelector, item, searchLower, column.Format))).ToList();
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

    private void UpdateDisplayedItems()
    {
        var items = FilteredItems;

        if (_currentSortColumn?.PropertySelector != null)
        {
            items = _currentSortDirection == SortDirection.Ascending
                ? items.OrderBy(_currentSortColumn.PropertySelector.Compile())
                : items.OrderByDescending(_currentSortColumn.PropertySelector.Compile());
        }

        DisplayedItems = items
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        Logger.Debug("Updated displayed items. Current page: {CurrentPage}, Items per page: {ItemsPerPage}",
            CurrentPage, ItemsPerPage);
    }

    private static bool MatchesSearchTerm(Expression<Func<TItem, object>>? selector, TItem item, string searchTerm,
        string format)
    {
        if (selector == null)
        {
            return false;
        }

        var compiledSelector = selector.Compile();
        var value = compiledSelector(item);

        if (value == null)
        {
            return false;
        }

        var valueString = value switch
        {
            DateTime dateTime => dateTime.ToString(format),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(format),
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
            _currentSortDirection = SortDirection.Ascending;
            _currentSortColumn = column;
        }

        Logger.Debug("Sorting by column: {Column}, Direction: {Direction}", column.Title, _currentSortDirection);
        UpdateDisplayedItems();
    }

    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
    }

    private string GetSortIconClass(SortDirection? sortDirection)
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
        if (CurrentPage > 1)
        {
            CurrentPage--;
            Logger.Debug("Navigated to previous page: {CurrentPage}", CurrentPage);
            UpdateDisplayedItems();
        }
    }

    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            Logger.Debug("Navigated to next page: {CurrentPage}", CurrentPage);
            UpdateDisplayedItems();
        }
    }

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
        if (selectAll)
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
        Logger.Debug("Export data triggered.");
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
        Logger.Debug("Edit item triggered for item: {Item}", item);
        if (OnEditItem.HasDelegate)
        {
            await OnEditItem.InvokeAsync(item);
        }
    }

    private async Task DeleteItemAsync(TItem item)
    {
        Logger.Debug("Delete item triggered for item: {Item}", item);
        if (OnDeleteItem.HasDelegate)
        {
            await OnDeleteItem.InvokeAsync(item);
        }
    }

    public void AddColumn(DataGridColumn<TItem> column)
    {
        if (_columns.Any(c => c.PropertyName == column.PropertyName))
        {
            Logger.Warning("Column with the property name {PropertyName} already exists and will not be added.",
                column.PropertyName);
            return;
        }

        _columns.Add(column);
        Logger.Debug("Column added: {PropertyName}", column.PropertyName);
        StateHasChanged();
    }

    public void ResetColumns()
    {
        _columns.Clear();
        Logger.Debug("Columns reset.");
        StateHasChanged();
    }

    private string GetFormattedValue(TItem item, DataGridColumn<TItem> column)
    {
        var value = column.PropertySelector?.Compile()(item);
        if (value is IFormattable formattable)
        {
            return formattable.ToString(column.Format, null);
        }

        return value?.ToString() ?? string.Empty;
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
        // The context menu will be handled by the DropBearContextMenu component
    }
}
