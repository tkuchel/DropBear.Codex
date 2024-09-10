#region

using System.Collections.ObjectModel;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an item in a context menu.
/// </summary>
public sealed class ContextMenuItem
{
    // Internally modifiable list for managing submenu items
    private readonly List<ContextMenuItem> _submenu = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class with default values.
    /// </summary>
    public ContextMenuItem() : this(string.Empty, string.Empty, false, false, new object())
    {
        // Default constructor chaining to main constructor
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class with specified values.
    /// </summary>
    /// <param name="text">The text displayed for the menu item.</param>
    /// <param name="iconClass">The CSS class for the icon associated with the menu item.</param>
    /// <param name="isSeparator">Indicates if this item is a separator.</param>
    /// <param name="isDanger">Indicates if this item should be styled as a danger item.</param>
    /// <param name="data">Custom data associated with the menu item.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text" /> is null or empty for a non-separator item.</exception>
    public ContextMenuItem(string text, string iconClass, bool isSeparator, bool isDanger, object data)
    {
        if (!isSeparator && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty for non-separator menu items.", nameof(text));
        }

        Text = text;
        IconClass = iconClass;
        IsSeparator = isSeparator;
        IsDanger = isDanger;
        Data = data ?? new object(); // Default data if null
    }

    /// <summary>
    ///     Gets the text displayed for the menu item.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the CSS class for the icon associated with the menu item.
    /// </summary>
    public string IconClass { get; }

    /// <summary>
    ///     Gets a value indicating whether this item is a separator.
    /// </summary>
    public bool IsSeparator { get; }

    /// <summary>
    ///     Gets a value indicating whether this item should be styled as a danger item.
    /// </summary>
    public bool IsDanger { get; }

    /// <summary>
    ///     Gets a value indicating whether this item has a submenu.
    /// </summary>
    public bool HasSubmenu => _submenu.Count > 0;

    /// <summary>
    ///     Gets the collection of submenu items as a read-only collection.
    /// </summary>
    public ReadOnlyCollection<ContextMenuItem> Submenu => _submenu.AsReadOnly();

    /// <summary>
    ///     Gets custom data associated with the menu item.
    /// </summary>
    public object Data { get; }

    /// <summary>
    ///     Adds a submenu item to the current context menu item.
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
