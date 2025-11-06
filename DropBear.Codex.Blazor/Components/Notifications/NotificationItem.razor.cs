#region

using System.Text.Json;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class NotificationItem : DropBearComponentBase
{
    [Parameter] public NotificationRecord Notification { get; set; } = null!;
    [Parameter] public EventCallback<Guid> OnMarkAsRead { get; set; }
    [Parameter] public EventCallback<Guid> OnDismiss { get; set; }

    private bool IsExpanded { get; set; }

    private string CssClasses => $"severity-{Notification.Severity.ToString().ToLower()} " +
                                 $"type-{Notification.Type.ToString().ToLower()} " +
                                 $"{(Notification.ReadAt.HasValue ? "read" : "unread")} " +
                                 $"{(Notification.DismissedAt.HasValue ? "dismissed" : "")} " +
                                 $"{(IsExpanded ? "expanded" : "")}";

    private SnackbarType IconType => Notification.Severity switch
    {
        NotificationSeverity.Success => SnackbarType.Success,
        NotificationSeverity.Information => SnackbarType.Information,
        NotificationSeverity.Warning => SnackbarType.Warning,
        NotificationSeverity.Error => SnackbarType.Error,
        NotificationSeverity.Critical => SnackbarType.Error, // Map Critical to Error since SnackbarType doesn't have Critical
        _ => SnackbarType.Information // Default for NotSpecified or any other case
    };

    private string RelativeTime
    {
        get
        {
            var timeSpan = DateTime.UtcNow - Notification.CreatedAt;

            return timeSpan.TotalDays > 7
                ? Notification.CreatedAt.ToString("MMM d, yyyy")
                : timeSpan.TotalDays > 1
                    ? $"{(int)timeSpan.TotalDays} days ago"
                    : timeSpan.TotalHours > 1
                        ? $"{(int)timeSpan.TotalHours} hours ago"
                        : timeSpan.TotalMinutes > 1
                            ? $"{(int)timeSpan.TotalMinutes} minutes ago"
                            : "just now";
        }
    }

    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        StateHasChanged();
    }

    private async Task HandleMarkAsRead()
    {
        if (OnMarkAsRead.HasDelegate)
        {
            await OnMarkAsRead.InvokeAsync(Notification.Id);
        }
    }

    private async Task HandleDismiss()
    {
        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync(Notification.Id);
        }
    }

    private string FormatData(IReadOnlyDictionary<string, object?> data)
    {
        try
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return "Unable to display additional data";
        }
    }
}
