#region

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents configuration for a data grid column,
///     including title, width, sorting/filtering options, and more.
/// </summary>
/// <typeparam name="TItem">Type of data items displayed by the grid.</typeparam>
public sealed class DataGridColumn<TItem>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataGridColumn{TItem}" /> class
    ///     with default property name and title.
    /// </summary>
    public DataGridColumn()
        : this("DefaultProperty", "Default Title")
    {
        // Default constructor
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataGridColumn{TItem}" /> class
    ///     with the specified column properties.
    /// </summary>
    /// <param name="propertyName">Name of the property on <typeparamref name="TItem" /> to bind.</param>
    /// <param name="title">Column title displayed in the header.</param>
    /// <param name="sortable">If true, the column supports sorting.</param>
    /// <param name="filterable">If true, the column supports filtering.</param>
    /// <param name="width">The width of the column in pixels (or another unit).</param>
    /// <param name="cssClass">Optional CSS class to apply to the column cells.</param>
    /// <param name="visible">If false, the column is hidden in the UI.</param>
    /// <param name="format">An optional format string for displaying values in the column cells.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="propertyName" /> is null or whitespace.
    /// </exception>
    public DataGridColumn(
        string propertyName,
        string title,
        bool sortable = false,
        bool filterable = false,
        double width = 100,
        string cssClass = "",
        bool visible = true,
        string format = "")
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("PropertyName cannot be null or empty.", nameof(propertyName));
        }

        PropertyName = propertyName;
        Title = title;
        Sortable = sortable;
        Filterable = filterable;
        Width = width;
        CssClass = cssClass;
        Visible = visible;
        Format = format;
    }

    /// <summary>
    ///     Gets the name of the property on <typeparamref name="TItem" /> that this column binds to.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    ///     Gets the title displayed in the column header.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Indicates whether the column supports sorting.
    /// </summary>
    public bool Sortable { get; }

    /// <summary>
    ///     Indicates whether the column supports filtering (e.g., by text input).
    /// </summary>
    public bool Filterable { get; }

    /// <summary>
    ///     Gets the width of the column (in pixels or another unit, depending on the styling).
    /// </summary>
    public double Width { get; }

    /// <summary>
    ///     Gets the CSS class applied to the column cells (if any).
    /// </summary>
    public string CssClass { get; }

    /// <summary>
    ///     Gets a value indicating whether the column is visible.
    ///     If false, the column may be hidden.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    ///     Gets the format string used to display column values.
    /// </summary>
    public string Format { get; }

    /// <summary>
    ///     Gets or sets the <see cref="Expression" /> used to select the property from <typeparamref name="TItem" />.
    ///     Useful for advanced binding or reflection scenarios.
    /// </summary>
    public Expression<Func<TItem, object>>? PropertySelector { get; init; }

    /// <summary>
    ///     Gets or sets a <see cref="RenderFragment{TItem}" /> that defines how to render cells of this column.
    ///     Overrides standard text-based rendering if set.
    /// </summary>
    public RenderFragment<TItem>? Template { get; init; }

    /// <summary>
    ///     Gets or sets a custom function to sort the <typeparamref name="TItem" /> collection when this column is sorted.
    ///     The boolean parameter typically indicates ascending/descending order.
    /// </summary>
    public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    /// <summary>
    ///     Gets or sets a custom function to filter the <typeparamref name="TItem" /> collection for this column.
    ///     The string parameter is usually the filter text.
    /// </summary>
    public Func<IEnumerable<TItem>, string, IEnumerable<TItem>>? CustomFilter { get; set; }

    /// <summary>
    ///     Gets or sets a <see cref="RenderFragment" /> for customizing the column header UI.
    /// </summary>
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    ///     Gets or sets a <see cref="RenderFragment" /> for customizing the column footer UI.
    /// </summary>
    public RenderFragment? FooterTemplate { get; set; }
}
