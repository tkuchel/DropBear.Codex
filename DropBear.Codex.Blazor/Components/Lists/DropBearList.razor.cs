#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Lists;

/// <summary>
///     A professional Blazor component that renders a collapsible list of items with modern styling,
///     animations, and comprehensive accessibility support.
/// </summary>
/// <typeparam name="T">The type of the data items in the list.</typeparam>
public sealed partial class DropBearList<T> : DropBearComponentBase where T : class
{
    #region Fields

    // Logger for this component
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearList<T>>();

    // Track if a re-render is needed
    private bool _shouldRender = true;

    // Backing fields for parameters to detect changes
    private IReadOnlyCollection<T>? _items;
    private string _title = string.Empty;
    private string _headerIcon = string.Empty;
    private string _headerColor = string.Empty;
    private bool _collapsedByDefault;
    private bool _isLoading;
    private bool _showItemCount;
    private string _emptyStateMessage = string.Empty;
    private bool _isItemInteractive;
    private int _loadingItemCount = 3;

    // Unique component identifier

    #endregion

    #region Properties

    /// <summary>
    ///     Tracks whether the list is currently collapsed.
    /// </summary>
    private bool IsCollapsed { get; set; }

    /// <summary>
    ///     Gets the unique component identifier.
    /// </summary>
    private new string ComponentId { get; } = Guid.NewGuid().ToString("N")[..8];

    #endregion

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Initialize collapsed state based on parameter
        IsCollapsed = CollapsedByDefault;
        Logger.Debug("DropBearList {ComponentId} initialized with Collapsed={Collapsed}", ComponentId, IsCollapsed);

        // Set flag to track initialization
        IsInitialized = true;
    }

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
    ///     Called after parameters are set.
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _shouldRender = true;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Toggles the collapsed state of the list and invokes the OnToggle callback.
    /// </summary>
    private async Task ToggleCollapse()
    {
        if (IsLoading)
        {
            return; // Don't allow toggling while loading
        }

        IsCollapsed = !IsCollapsed;
        Logger.Debug("List {Title} ({ComponentId}) collapsed state toggled to {IsCollapsed}",
            Title, ComponentId, IsCollapsed);

        _shouldRender = true;

        // Invoke the callback if one is set
        if (OnToggle.HasDelegate)
        {
            await OnToggle.InvokeAsync(IsCollapsed);
        }
    }

    /// <summary>
    ///     Handles keyboard navigation for the header.
    /// </summary>
    /// <param name="e">The keyboard event args.</param>
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
        {
            // e.PreventDefault();
            await ToggleCollapse();
        }
    }

    /// <summary>
    ///     Handles item click events.
    /// </summary>
    /// <param name="item">The clicked item.</param>
    /// <param name="index">The item index.</param>
    private async Task HandleItemClick(T item, int index)
    {
        if (!IsItemInteractive || IsLoading)
        {
            return;
        }

        Logger.Debug("Item clicked: {Item} at index {Index}", item, index);

        if (OnItemClick.HasDelegate)
        {
            await OnItemClick.InvokeAsync(new ItemClickEventArgs<T>(item, index));
        }
    }

    /// <summary>
    ///     Handles keyboard navigation for list items.
    /// </summary>
    /// <param name="e">The keyboard event args.</param>
    /// <param name="item">The item.</param>
    /// <param name="index">The item index.</param>
    private async Task HandleItemKeyDown(KeyboardEventArgs e, T item, int index)
    {
        if (!IsItemInteractive || IsLoading)
        {
            return;
        }

        if (e.Key is "Enter" or " ")
        {
            // e.PreventDefault();
            await HandleItemClick(item, index);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Generates dynamic CSS styles for the component.
    /// </summary>
    /// <returns>A CSS style string.</returns>
    private string GetDynamicStyles()
    {
        var styles = new List<string>();

        if (!string.IsNullOrEmpty(HeaderColor))
        {
            styles.Add($"--clr-surface-primary: {HeaderColor}");
        }

        return string.Join("; ", styles);
    }

    /// <summary>
    ///     Determines if two collections are equal.
    /// </summary>
    /// <param name="a">The first collection.</param>
    /// <param name="b">The second collection.</param>
    /// <returns>True if the collections are equal, false otherwise.</returns>
    private static bool CollectionsEqual(IReadOnlyCollection<T>? a, IReadOnlyCollection<T>? b)
    {
        // Same reference or both null
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        // One is null but the other isn't
        if (a == null || b == null)
        {
            return false;
        }

        // Different counts
        if (a.Count != b.Count)
        {
            return false;
        }

        // For small collections, simple comparison
        if (a.Count <= 16)
        {
            return a.SequenceEqual(b);
        }

        // For larger collections, use optimized comparison
        return a.SequenceEqual(b);
    }

    #endregion

    #region Parameters

    /// <summary>
    ///     The collection of items to render in the list.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<T>? Items
    {
        get => _items;
        set
        {
            if (!CollectionsEqual(_items, value))
            {
                _items = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The title to display in the header of the list.
    /// </summary>
    [Parameter]
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     An optional icon (e.g., FontAwesome class) to display in the header.
    /// </summary>
    [Parameter]
    public string HeaderIcon
    {
        get => _headerIcon;
        set
        {
            if (_headerIcon != value)
            {
                _headerIcon = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The color to use for the list header background.
    /// </summary>
    [Parameter]
    public string HeaderColor
    {
        get => _headerColor;
        set
        {
            if (_headerColor != value)
            {
                _headerColor = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     A template to render each item in the list.
    /// </summary>
    [Parameter]
    public RenderFragment<T>? ItemTemplate { get; set; }

    /// <summary>
    ///     If true, the list is collapsed by default on initial render.
    /// </summary>
    [Parameter]
    public bool CollapsedByDefault
    {
        get => _collapsedByDefault;
        set
        {
            if (_collapsedByDefault != value)
            {
                _collapsedByDefault = value;
                if (!IsInitialized)
                {
                    IsCollapsed = value;
                }

                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Indicates whether the list is currently loading data.
    /// </summary>
    [Parameter]
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Whether to show the item count in the header.
    /// </summary>
    [Parameter]
    public bool ShowItemCount
    {
        get => _showItemCount;
        set
        {
            if (_showItemCount != value)
            {
                _showItemCount = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Custom message to display when the list is empty.
    /// </summary>
    [Parameter]
    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        set
        {
            if (_emptyStateMessage != value)
            {
                _emptyStateMessage = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Whether list items are interactive (clickable).
    /// </summary>
    [Parameter]
    public bool IsItemInteractive
    {
        get => _isItemInteractive;
        set
        {
            if (_isItemInteractive != value)
            {
                _isItemInteractive = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The number of skeleton items to show when loading.
    /// </summary>
    [Parameter]
    public int LoadingItemCount
    {
        get => _loadingItemCount;
        set
        {
            if (_loadingItemCount != value)
            {
                _loadingItemCount = Math.Max(1, Math.Min(10, value)); // Clamp between 1-10
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     An event callback invoked whenever the list is collapsed or expanded.
    ///     The boolean argument indicates whether the list is currently collapsed.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnToggle { get; set; }

    /// <summary>
    ///     An event callback invoked when an item is clicked (only if IsItemInteractive is true).
    /// </summary>
    [Parameter]
    public EventCallback<ItemClickEventArgs<T>> OnItemClick { get; set; }

    /// <summary>
    ///     Flag to track if the component has been initialized.
    /// </summary>
    private bool IsInitialized { get; set; }

    #endregion
}

/// <summary>
///     Event arguments for item click events.
/// </summary>
/// <typeparam name="T">The type of the clicked item.</typeparam>
public sealed class ItemClickEventArgs<T>
{
    /// <summary>
    ///     Initializes a new instance of the ItemClickEventArgs class.
    /// </summary>
    /// <param name="item">The clicked item.</param>
    /// <param name="index">The index of the clicked item.</param>
    public ItemClickEventArgs(T item, int index)
    {
        Item = item;
        Index = index;
    }

    /// <summary>
    ///     Gets the clicked item.
    /// </summary>
    public T Item { get; }

    /// <summary>
    ///     Gets the index of the clicked item.
    /// </summary>
    public int Index { get; }
}
