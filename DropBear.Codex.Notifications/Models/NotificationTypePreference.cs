using DropBear.Codex.Notifications.Enums;

namespace DropBear.Codex.Notifications.Models;

public class NotificationTypePreference
{
    public bool Enabled { get; set; } = true;
    public NotificationSeverity MinimumSeverity { get; set; } = NotificationSeverity.Information;
}
