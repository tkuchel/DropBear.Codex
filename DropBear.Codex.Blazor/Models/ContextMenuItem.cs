#region

using System.Collections.ObjectModel;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an item in a context menu, which can also hold submenu items.
/// </summary>
public sealed class ContextMenuItem
{
    // Internally maintained list for submenu items.
    private readonly List<ContextMenuItem> _submenu = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class
    ///     with default values (empty text, icon, etc.).
    /// </summary>
    public ContextMenuItem()
        : this(string.Empty, string.Empty, false, false, new object())
    {
        // Default constructor
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class
    ///     with specified values.
    /// </summary>
    /// <param name="text">The display text for the menu item.</param>
    /// <param name="iconClass">The CSS class for an optional icon.</param>
    /// <param name="isSeparator">If true, this item is just a separator line.</param>
    /// <param name="isDanger">If true, this item is styled in a "danger" look (e.g., red).</param>
    /// <param name="data">Custom data associated with this item.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="text" /> is null or empty for a non-separator item.
    /// </exception>
    public ContextMenuItem(string text, string iconClass, bool isSeparator, bool isDanger, object? data)
    {
        if (!isSeparator && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty for non-separator menu items.", nameof(text));
        }

        Text = text;
        IconClass = iconClass;
        IsSeparator = isSeparator;
        IsDanger = isDanger;
        Data = data ?? new object(); // fallback if null
    }

    /// <summary>
    ///     Gets the display text for the menu item.
    ///     May be empty if <see cref="IsSeparator" /> is true.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the CSS class for an optional icon. If empty, no icon is displayed.
    /// </summary>
    public string IconClass { get; }

    /// <summary>
    ///     Indicates whether this item is just a separator line (no action).
    /// </summary>
    public bool IsSeparator { get; }

    /// <summary>
    ///     Indicates whether this item should be styled as a danger item (e.g., red text).
    /// </summary>
    public bool IsDanger { get; }

    /// <summary>
    ///     Gets a value indicating whether this item has any submenu items.
    /// </summary>
    public bool HasSubmenu => _submenu.Count > 0;

    /// <summary>
    ///     Gets the submenu items as a read-only collection.
    ///     Use <see cref="AddSubmenuItem(ContextMenuItem)" /> to add items.
    /// </summary>
    public ReadOnlyCollection<ContextMenuItem> Submenu => _submenu.AsReadOnly();

    /// <summary>
    ///     Gets custom data associated with the menu item, which can store
    ///     arbitrary context info for event handlers.
    /// </summary>
    public object Data { get; }

    /// <summary>
    ///     Adds a new item to this menu item's submenu.
    /// </summary>
    /// <param name="subItem">The submenu item to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="subItem" /> is null.</exception>
    public void AddSubmenuItem(ContextMenuItem subItem)
    {
        if (subItem == null)
        {
            throw new ArgumentNullException(nameof(subItem), "Submenu item cannot be null.");
        }

        _submenu.Add(subItem);
    }
}
