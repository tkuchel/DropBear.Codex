#region

using System.Linq.Expressions;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Grids;

public sealed partial class DropBearDataGridColumn<TItem> : DropBearComponentBase
{
    private bool _isInitialized;

    [CascadingParameter] private DropBearDataGrid<TItem> ParentGrid { get; set; } = default!;

    [Parameter] public string PropertyName { get; set; } = string.Empty;
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public Expression<Func<TItem, object>> PropertySelector { get; set; } = default!;
    [Parameter] public bool Sortable { get; set; } = true; // Defaults to true as sorting is common.
    [Parameter] public bool Filterable { get; set; } = true; // Defaults to true as filtering is common.
    [Parameter] public string Format { get; set; } = string.Empty;
    [Parameter] public int Width { get; set; } = 150; // Default width set to a common size.
    [Parameter] public RenderFragment<TItem>? Template { get; set; }
    [Parameter] public Func<IEnumerable<TItem>, bool, IEnumerable<TItem>>? CustomSort { get; set; }

    protected override void OnInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        if (ParentGrid is null)
        {
            throw new InvalidOperationException(
                $"{nameof(DropBearDataGridColumn<TItem>)} must be used within a {nameof(DropBearDataGrid<TItem>)}");
        }

        var column = new DataGridColumn<TItem>
        {
            PropertyName = PropertyName,
            Title = Title,
            PropertySelector = PropertySelector,
            Sortable = Sortable,
            Filterable = Filterable,
            Format = Format,
            Width = Width,
            Template = Template,
            CustomSort = CustomSort
        };

        ParentGrid.AddColumn(column);
        _isInitialized = true;
    }
}
