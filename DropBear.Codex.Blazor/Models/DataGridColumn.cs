#region

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a column in a data grid.
/// </summary>
/// <typeparam name="TItem">The type of the data items.</typeparam>
public sealed class DataGridColumn<TItem>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataGridColumn{TItem}" /> class with default values.
    /// </summary>
    public DataGridColumn()
        : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataGridColumn{TItem}" /> class with specified values.
    /// </summary>
    /// <param name="propertyName">The name of the property that the column represents.</param>
    /// <param name="title">The title of the column.</param>
    /// <param name="sortable">Indicates if the column is sortable.</param>
    /// <param name="filterable">Indicates if the column is filterable.</param>
    /// <param name="width">The width of the column.</param>
    /// <param name="cssClass">The CSS class to apply to the column.</param>
    /// <param name="visible">Indicates if the column is visible.</param>
    /// <param name="format">The format string for displaying values in the column.</param>
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
    ///     Gets the name of the property that the column represents.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    ///     Gets the title of the column.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets a value indicating whether the column is sortable.
    /// </summary>
    public bool Sortable { get; }

    /// <summary>
    ///     Gets a value indicating whether the column is filterable.
    /// </summary>
    public bool Filterable { get; }

    /// <summary>
    ///     Gets the width of the column.
    /// </summary>
    public double Width { get; }

    /// <summary>
    ///     Gets the CSS class to apply to the column.
    /// </summary>
    public string CssClass { get; }

    /// <summary>
    ///     Gets a value indicating whether the column is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    ///     Gets the format string for displaying values in the column.
    /// </summary>
    public string Format { get; }

    /// <summary>
    ///     Gets or sets the expression for selecting the property from the data item.
    /// </summary>
    public Expression<Func<TItem, object>>? PropertySelector { get; set; }

    /// <summary>
    ///     Gets or sets the template for rendering the column content.
    /// </summary>
    public RenderFragment<TItem>? Template { get; set; }

    /// <summary>
    ///     Gets or sets a custom sort function for the column.
    /// </summary>
    public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    /// <summary>
    ///     Gets or sets a custom filter function for the column.
    /// </summary>
    public Func<IEnumerable<TItem>, string, IEnumerable<TItem>>? CustomFilter { get; set; }

    /// <summary>
    ///     Gets or sets the template for rendering the column header.
    /// </summary>
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the template for rendering the column footer.
    /// </summary>
    public RenderFragment? FooterTemplate { get; set; }
}
