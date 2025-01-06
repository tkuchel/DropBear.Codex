#region

using System.Linq.Expressions;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a column definition (metadata) for a report viewer or data grid.
/// </summary>
/// <typeparam name="TItem">Type of the data items displayed in the column.</typeparam>
public sealed class ColumnDefinition<TItem>
{
    /// <summary>
    ///     Gets or sets the property name of the data item that this column is bound to.
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the display name of the column (the header text).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether the column is visible.
    ///     If false, the column may be hidden in the UI.
    /// </summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    ///     Gets or sets a filter value for this column.
    ///     Typically used in UI to filter rows by matching text.
    /// </summary>
    public string FilterValue { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets an expression that selects the property
    ///     from <typeparamref name="TItem" /> for binding or sorting.
    /// </summary>
    public Expression<Func<TItem, object>> PropertySelector { get; init; } = null!;
}
