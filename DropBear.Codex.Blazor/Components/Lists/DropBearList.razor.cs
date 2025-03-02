#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Lists;

/// <summary>
///     A Blazor component that renders a list of items with an expandable/collapsible header.
///     Optimized for Blazor Server with efficient rendering and state management.
/// </summary>
/// <typeparam name="T">The type of the data items in the list.</typeparam>
public sealed partial class DropBearList<T> : DropBearComponentBase where T : class
{
    #region Properties

    /// <summary>
    ///     Tracks whether the list is currently collapsed.
    /// </summary>
    private bool IsCollapsed { get; set; }

    #endregion

    #region Fields

    // Logger for this component.
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearList<T>>();

    // Track if a re-render is needed
    private bool _shouldRender = true;

    // Backing fields for parameters to detect changes
    private IReadOnlyCollection<T>? _items;
    private string _title = string.Empty;
    private string _headerIcon = string.Empty;
    private string _headerColor = "var(--clr-grey-500, #95989C)";
    private bool _collapsedByDefault;

    #endregion

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Initialize collapsed state based on parameter.
        IsCollapsed = CollapsedByDefault;
        Logger.Debug("DropBearList initialized with Collapsed={Collapsed}", IsCollapsed);

        // Extra runtime validation for the required template.
        if (ItemTemplate is null)
        {
            Logger.Error("ItemTemplate is required but was not provided.");
            throw new InvalidOperationException($"{nameof(ItemTemplate)} cannot be null.");
        }
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
        // Mark for re-render if any parameters that affect the render have changed
        _shouldRender = true;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Toggles the collapsed state of the list and invokes the OnToggle callback.
    /// </summary>
    private async Task ToggleCollapse()
    {
        // Toggle the collapsed flag.
        IsCollapsed = !IsCollapsed;
        Logger.Debug("List {Title} collapsed state toggled to {IsCollapsed}.", Title, IsCollapsed);

        // Mark for re-render
        _shouldRender = true;

        // Invoke the callback if one is set.
        if (OnToggle.HasDelegate)
        {
            await OnToggle.InvokeAsync(IsCollapsed);
        }
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

        // For larger collections, we might want to optimize further
        // based on the actual needs, but for now:
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
    ///     The color to use for the list header. Defaults to a CSS variable for consistent styling.
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
    [EditorRequired]
    public RenderFragment<T> ItemTemplate { get; set; } = null!;

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
    ///     An event callback invoked whenever the list is collapsed or expanded.
    ///     The boolean argument indicates whether the list is currently collapsed.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnToggle { get; set; }

    /// <summary>
    ///     Flag to track if the component has been initialized.
    /// </summary>
    private bool IsInitialized { get; set; }

    #endregion
}
