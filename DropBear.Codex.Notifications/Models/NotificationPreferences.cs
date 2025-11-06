using System.Text.Json;

namespace DropBear.Codex.Notifications.Models;

public class NotificationPreferences
{
    public Guid UserId { get; set; }
    public bool EnableToastNotifications { get; set; } = true;
    public bool EnableInboxNotifications { get; set; } = true;
    public bool EnableEmailNotifications { get; set; } = false;

    public IDictionary<string, NotificationTypePreference> TypePreferences { get; set; } = new Dictionary<string, NotificationTypePreference>(StringComparer.Ordinal);
    public string SerializedTypePreferences
    {
        get => JsonSerializer.Serialize(TypePreferences);
        set => TypePreferences = JsonSerializer.Deserialize<Dictionary<string, NotificationTypePreference>>(value) ?? new Dictionary<string, NotificationTypePreference>(StringComparer.Ordinal);
    }
}
