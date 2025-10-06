#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Notifications.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class NotificationFilter : DropBearComponentBase
{
    [Parameter] public Codex.Notifications.Filters.NotificationFilter CurrentFilter { get; set; } = null!;
    [Parameter] public EventCallback<Codex.Notifications.Filters.NotificationFilter> OnFilterChange { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private Codex.Notifications.Filters.NotificationFilter LocalFilter { get; set; } = new();

    // Binding properties for select dropdowns
    private string? SelectedSeverity
    {
        get => LocalFilter.Severity?.ToString() ?? "";
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                LocalFilter.Severity = null;
            }
            else if (Enum.TryParse<NotificationSeverity>(value, out var severity))
            {
                LocalFilter.Severity = severity;
            }
        }
    }

    private string? SelectedType
    {
        get => LocalFilter.Type?.ToString() ?? "";
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                LocalFilter.Type = null;
            }
            else if (Enum.TryParse<NotificationType>(value, out var type))
            {
                LocalFilter.Type = type;
            }
        }
    }

    // Date range properties
    private string? FromDateValue
    {
        get => LocalFilter.FromDate?.ToString("yyyy-MM-dd") ?? "";
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                LocalFilter.FromDate = null;
            }
            else if (DateTime.TryParse(value, out var date))
            {
                LocalFilter.FromDate = date;
            }
        }
    }

    private string? ToDateValue
    {
        get => LocalFilter.ToDate?.ToString("yyyy-MM-dd") ?? "";
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                LocalFilter.ToDate = null;
            }
            else if (DateTime.TryParse(value, out var date))
            {
                // Set to end of day
                LocalFilter.ToDate = date.AddDays(1).AddTicks(-1);
            }
        }
    }

    private void UpdateFromDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            LocalFilter.FromDate = null;
        }
        else if (DateTime.TryParse(dateStr, out var date))
        {
            LocalFilter.FromDate = date;
        }
    }

    private void UpdateToDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            LocalFilter.ToDate = null;
        }
        else if (DateTime.TryParse(dateStr, out var date))
        {
            // Set to end of day for inclusive filtering
            LocalFilter.ToDate = date.AddDays(1).AddTicks(-1);
        }
    }

    protected override void OnInitialized()
    {
        // Clone the current filter to avoid modifying it directly
        LocalFilter = new Codex.Notifications.Filters.NotificationFilter
        {
            UserId = CurrentFilter.UserId,
            IsRead = CurrentFilter.IsRead,
            IsDismissed = CurrentFilter.IsDismissed,
            Type = CurrentFilter.Type,
            Severity = CurrentFilter.Severity,
            FromDate = CurrentFilter.FromDate,
            ToDate = CurrentFilter.ToDate,
            SearchText = CurrentFilter.SearchText,
            PageNumber = CurrentFilter.PageNumber,
            PageSize = CurrentFilter.PageSize,
            SortBy = CurrentFilter.SortBy,
            SortDescending = CurrentFilter.SortDescending
        };
    }

    private void SetReadStatus(bool? isRead)
    {
        LocalFilter.IsRead = isRead;
    }

    private async Task ApplyFilter()
    {
        // Reset to page 1 when filter changes
        LocalFilter.PageNumber = 1;

        if (OnFilterChange.HasDelegate)
        {
            await OnFilterChange.InvokeAsync(LocalFilter);
        }

        await CloseFilter();
    }

    private void ResetFilter()
    {
        LocalFilter =
            new Codex.Notifications.Filters.NotificationFilter
            {
                UserId = CurrentFilter.UserId, PageSize = CurrentFilter.PageSize
            };
    }

    private async Task CloseFilter()
    {
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }
}
