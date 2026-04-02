#region

using System.Text.Json;
using DropBear.Codex.Notifications.Enums;

#endregion

namespace DropBear.Codex.Notifications.Entities;

/// <summary>
///     Represents a persisted notification record in the database.
///     SECURITY NOTE: Message and Title fields should be sanitized before storage
///     to prevent stored XSS attacks. Use System.Net.WebUtility.HtmlEncode() or
///     a dedicated sanitization library before saving user-generated content.
/// </summary>
public class NotificationRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the notification message.
    /// SECURITY: Should be HTML-encoded before storage if user-generated.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional notification title.
    /// SECURITY: Should be HTML-encoded before storage if user-generated.
    /// </summary>
    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public string? SerializedData { get; set; }

    // Navigation property (if using EF Core)
    // public virtual User? User { get; set; } //Todo: Implement User entity or link to existing one

    // Helper methods for data
    public IReadOnlyDictionary<string, object?> GetData()
    {
        if (string.IsNullOrEmpty(SerializedData))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(
                   SerializedData,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public void SetData(IDictionary<string, object?> data)
    {
        SerializedData = JsonSerializer.Serialize(data);
    }
}
