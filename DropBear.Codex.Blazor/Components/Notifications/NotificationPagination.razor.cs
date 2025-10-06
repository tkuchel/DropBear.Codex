#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class NotificationPagination : DropBearComponentBase
{
    [Parameter] public int CurrentPage { get; set; } = 1;
    [Parameter] public int PageSize { get; set; } = 10;
    [Parameter] public int TotalItems { get; set; }
    [Parameter] public EventCallback<int> OnPageChange { get; set; }

    private int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    private List<int> GetVisiblePages()
    {
        var result = new List<int>();

        // Always show first page
        result.Add(1);

        // Calculate range of pages to show around current page
        var start = Math.Max(2, CurrentPage - 1);
        var end = Math.Min(TotalPages - 1, CurrentPage + 1);

        // Add ellipsis if there's a gap after page 1
        if (start > 2)
        {
            result.Add(-1); // -1 represents ellipsis
        }

        // Add pages in range
        for (var i = start; i <= end; i++)
        {
            result.Add(i);
        }

        // Add ellipsis if there's a gap before last page
        if (end < TotalPages - 1 && TotalPages > 1)
        {
            result.Add(-1); // -1 represents ellipsis
        }

        // Always show last page if there is more than one page
        if (TotalPages > 1 && !result.Contains(TotalPages))
        {
            result.Add(TotalPages);
        }

        return result;
    }

    private async Task OnPageChangeInternal(int page)
    {
        if (page < 1 || page > TotalPages || page == CurrentPage)
        {
            return;
        }

        if (OnPageChange.HasDelegate)
        {
            await OnPageChange.InvokeAsync(page);
        }
    }
}
