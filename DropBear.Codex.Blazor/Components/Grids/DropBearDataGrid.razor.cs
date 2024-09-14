#region

using System.Collections.ObjectModel;
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
public sealed partial class DropBearDataGrid<TItem> : DropBearComponentBase, IDisposable
{
    private const int DebounceDelay = 300; // milliseconds
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearDataGrid<TItem>>();
    private readonly List<DataGridColumn<TItem>> _columns = new();
    private DataGridColumn<TItem> _currentSortColumn = new();
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private Timer? _debounceTimer;
    private bool _isInitialized;
    private bool _isSearching;
    private ElementReference _searchInput;
    private List<TItem>? _selectedItems;

    [Parameter] public IEnumerable<TItem> Items { get; set; } = new List<TItem>();
    [Parameter] public string Title { get; set; } = "Data Grid";
    [Parameter] public bool EnableSearch { get; set; } = true;
    [Parameter] public bool EnablePagination { get; set; } = true;
    [Parameter] public int ItemsPerPage { get; set; } = 10;
    [Parameter] public bool EnableMultiSelect { get; set; }
    [Parameter] public bool AllowAdd { get; set; } = true;
    [Parameter] public bool AllowEdit { get; set; } = true;
    [Parameter] public bool AllowDelete { get; set; } = true;
    [Parameter] public bool AllowExport { get; set; } = false;
    [Parameter] public EventCallback<List<TItem>> OnExportData { get; set; }
    [Parameter] public EventCallback<TItem> OnAddItem { get; set; }
    [Parameter] public EventCallback<TItem> OnEditItem { get; set; }
    [Parameter] public EventCallback<TItem> OnDeleteItem { get; set; }
    [Parameter] public EventCallback<List<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClicked { get; set; }
    [Parameter] public RenderFragment Columns { get; set; } = default!;
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }

    private string SearchTerm { get; set; } = string.Empty;
    private int CurrentPage { get; set; } = 1;
    private int TotalPages => (int)Math.Ceiling(FilteredItems.Count() / (double)ItemsPerPage);

    private List<TItem> SelectedItems
    {
        get => _selectedItems ??= new List<TItem>();
        // ReSharper disable once UnusedMember.Local
        set => _selectedItems = value;
    }

    private IEnumerable<TItem> FilteredItems { get; set; } = Enumerable.Empty<TItem>();
    private IEnumerable<TItem> DisplayedItems { get; set; } = Enumerable.Empty<TItem>();

    private IReadOnlyCollection<int> ItemsPerPageOptions { get; } =
        new ReadOnlyCollection<int>(new List<int> { 10, 25, 50, 100 });

    private bool IsLoading { get; set; } = true;
    private bool HasData => FilteredItems.Any();

    private int TotalColumnCount =>
        _columns.Count + (EnableMultiSelect ? 1 : 0) + (AllowEdit || AllowDelete ? 1 : 0);

    public IReadOnlyList<DataGridColumn<TItem>> GetColumns => _columns.AsReadOnly();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _selectedItems ??= new List<TItem>();

        await LoadDataAsync();
        _isInitialized = true;
    }

    protected override bool ShouldRender()
    {
        return _isInitialized;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();
            Logger.Information("Loading data for DropBearDataGrid.");

            await Task.Delay(500); // Simulate data loading

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
        _debounceTimer = new Timer(DebounceSearch, null, DebounceDelay, Timeout.Infinite);
    }

    private async void DebounceSearch(object? state)
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await PerformSearch();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            // Handle or log exception
            Console.WriteLine($"An error occurred during the debounce search: {ex.Message}");
        }
    }

    private async Task PerformSearch()
    {
        _isSearching = true;
        StateHasChanged();
        Logger.Information("Performing search with term: {SearchTerm}", SearchTerm);

        try
        {
            await Task.Delay(200); // Simulate search delay

            FilteredItems = string.IsNullOrWhiteSpace(SearchTerm)
                ? Items.ToList()
                : Items.Where(item => _columns.Any(column =>
                    MatchesSearchTerm(column.PropertySelector?.Compile()(item), SearchTerm, column.Format)
                )).ToList();

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
            await _searchInput.FocusAsync();
            StateHasChanged();
        }
    }

    private void UpdateDisplayedItems()
    {
        DisplayedItems = FilteredItems
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage);
        Logger.Debug("Updated displayed items. Current page: {CurrentPage}, Items per page: {ItemsPerPage}",
            CurrentPage, ItemsPerPage);
    }

    private static bool MatchesSearchTerm(object? value, string searchTerm, string format)
    {
        if (value is null || string.IsNullOrWhiteSpace(searchTerm))
        {
            return false;
        }

        var valueString = value switch
        {
            DateTime dateTime => FormatDateTime(dateTime, format),
            DateTimeOffset dateTimeOffset => FormatDateTime(dateTimeOffset.DateTime, format),
            _ => value.ToString()
        };

        return valueString?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FormatDateTime(DateTime dateTime, string format)
    {
        return string.IsNullOrEmpty(format) ? dateTime.ToString("d") : dateTime.ToString(format);
    }

    private void SortBy(DataGridColumn<TItem> column)
    {
        if (!column.Sortable)
        {
            return;
        }

        _currentSortDirection = _currentSortColumn == column
            ? _currentSortDirection is SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending
            : SortDirection.Ascending;

        _currentSortColumn = column;

        if (column.PropertySelector != null)
        {
            FilteredItems = column.CustomSort is not null
                ? column.CustomSort(FilteredItems, _currentSortDirection is SortDirection.Ascending)
                : FilteredItems.OrderBy(column.PropertySelector.Compile())
                    .ToList();
        }

        Logger.Information("Sorted by column: {Column}, Direction: {Direction}", column.PropertyName,
            _currentSortDirection);

        UpdateDisplayedItems();
    }

    private SortDirection? GetSortDirection(DataGridColumn<TItem> column)
    {
        return _currentSortColumn == column ? _currentSortDirection : null;
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

    private void ToggleSelection(TItem item, bool isSelected)
    {
        if (isSelected)
        {
            if (!SelectedItems.Contains(item))
            {
                SelectedItems.Add(item);
                Logger.Debug("Item selected: {Item}", item);
            }
        }
        else
        {
            SelectedItems.Remove(item);
            Logger.Debug("Item deselected: {Item}", item);
        }

        _ = OnSelectionChanged.InvokeAsync(SelectedItems.ToList());
        StateHasChanged();
    }

    private void ToggleSelectAll(bool selectAll)
    {
        SelectedItems.Clear();
        if (selectAll)
        {
            SelectedItems.AddRange(Items);
            Logger.Debug("All items selected.");
        }
        else
        {
            Logger.Debug("All items deselected.");
        }

        _ = OnSelectionChanged.InvokeAsync(SelectedItems.ToList());
        StateHasChanged();
    }

    private async Task ExportData()
    {
        Logger.Information("Export data triggered.");
        await OnExportData.InvokeAsync(default!);
    }

    private async Task AddItem()
    {
        Logger.Information("Add item triggered.");
        await OnAddItem.InvokeAsync(default!);
    }

    private async Task EditItem(TItem item)
    {
        Logger.Information("Edit item triggered for item: {Item}", item);
        await OnEditItem.InvokeAsync(item);
    }

    private async Task DeleteItem(TItem item)
    {
        Logger.Information("Delete item triggered for item: {Item}", item);
        await OnDeleteItem.InvokeAsync(item);
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
        Logger.Information("Columns reset.");
        StateHasChanged();
    }

    private static string GetFormattedValue(TItem item, DataGridColumn<TItem> column)
    {
        var value = column.PropertySelector?.Compile()(item);
        if (value is IFormattable formattable)
        {
            return formattable.ToString(column.Format, null);
        }

        return value?.ToString() ?? string.Empty;
    }

    private async Task HandleRowClick(TItem item)
    {
        Logger.Debug("Row clicked for item: {Item}", item);
        await OnRowClicked.InvokeAsync(item);
    }

    // ReSharper disable once UnusedParameter.Local
    private async Task HandleRowContextMenu(MouseEventArgs e, TItem item)
    {
        await HandleRowClick(item);
        // The context menu will be handled by the DropBearContextMenu component
    }
}
