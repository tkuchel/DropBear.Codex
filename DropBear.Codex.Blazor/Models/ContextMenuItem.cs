#region

using System.Collections.ObjectModel;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an item in a context menu.
/// </summary>
public sealed class ContextMenuItem
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class.
    /// </summary>
    public ContextMenuItem() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuItem" /> class with specified values.
    /// </summary>
    /// <param name="text">The text displayed for the menu item.</param>
    /// <param name="iconClass">The CSS class for the icon associated with the menu item.</param>
    /// <param name="isSeparator">Indicates if this item is a separator.</param>
    /// <param name="isDanger">Indicates if this item should be styled as a danger item.</param>
    /// <param name="data">Custom data associated with the menu item.</param>
    public ContextMenuItem(string text, string iconClass, bool isSeparator, bool isDanger, object data)
    {
        Text = text;
        IconClass = iconClass;
        IsSeparator = isSeparator;
        IsDanger = isDanger;
        Data = data;
    }

    /// <summary>
    ///     Gets or sets the text displayed for the menu item.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the CSS class for the icon associated with the menu item.
    /// </summary>
    public string IconClass { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether this item is a separator.
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this item should be styled as a danger item.
    /// </summary>
    public bool IsDanger { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this item has a submenu.
    /// </summary>
    public bool HasSubmenu => Submenu.Count > 0;

    /// <summary>
    ///     Gets or sets the collection of submenu items.
    /// </summary>
    public Collection<ContextMenuItem> Submenu { get; set; } = new();

    /// <summary>
    ///     Gets or sets custom data associated with the menu item.
    /// </summary>
    public object Data { get; set; } = new();
}
