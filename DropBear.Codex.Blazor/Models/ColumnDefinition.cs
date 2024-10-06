#region

using System.Linq.Expressions;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a column definition for the report viewer.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public class ColumnDefinition<TItem>
{
    /// <summary>
    ///     Gets or sets the property name of the data item.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the display name of the column.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether the column is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the filter value for the column.
    /// </summary>
    public string FilterValue { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the property selector expression for the column.
    /// </summary>
    public Expression<Func<TItem, object>> PropertySelector { get; set; } = default!;
}
