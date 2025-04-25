#region

using DropBear.Codex.Notifications.Enums;

#endregion

namespace DropBear.Codex.Notifications.Filters;

public class NotificationFilter
{
    public Guid UserId { get; set; }
    public bool? IsRead { get; set; }
    public bool? IsDismissed { get; set; }
    public NotificationType? Type { get; set; }
    public NotificationSeverity? Severity { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchText { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}
