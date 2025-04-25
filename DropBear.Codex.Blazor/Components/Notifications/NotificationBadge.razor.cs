#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Interfaces;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class NotificationBadge : DropBearComponentBase, IDisposable
{
    private bool _disposed;
    private Timer? _refreshTimer;
    [Inject] private INotificationCenterService NotificationService { get; set; } = null!;
    [Parameter] public Guid UserId { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    private int UnreadCount { get; set; }

    public void Dispose()
    {
        _disposed = true;

        // Clean up timer
        _refreshTimer?.Dispose();

        // Unsubscribe from events
        if (NotificationService != null)
        {
            NotificationService.OnNotificationReceived -= HandleNotificationReceived;
            NotificationService.OnNotificationRead -= HandleNotificationRead;
            NotificationService.OnNotificationDismissed -= HandleNotificationDismissed;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Subscribe to notification events
        NotificationService.OnNotificationReceived += HandleNotificationReceived;
        NotificationService.OnNotificationRead += HandleNotificationRead;
        NotificationService.OnNotificationDismissed += HandleNotificationDismissed;

        // Fetch initial count
        await UpdateUnreadCountAsync();

        // Set up timer to periodically refresh the count (every 2 minutes)
        _refreshTimer = new Timer(async _ => await RefreshCountAsync(), null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
    }

    private async Task UpdateUnreadCountAsync()
    {
        try
        {
            UnreadCount = await NotificationService.GetUnreadCountAsync(UserId);
        }
        catch (Exception ex)
        {
            LogError("Failed to get unread notification count", ex);
        }
    }

    private async Task RefreshCountAsync()
    {
        if (_disposed)
        {
            return;
        }

        await UpdateUnreadCountAsync();

        if (_disposed)
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleNotificationReceived(NotificationRecord notification)
    {
        if (notification.UserId != UserId || _disposed)
        {
            return;
        }

        // Increment count only if it's a new unread notification
        if (notification.ReadAt == null && notification.DismissedAt == null)
        {
            UnreadCount++;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleNotificationRead(Guid notificationId)
    {
        if (_disposed)
        {
            return;
        }

        // Refresh the count when a notification is marked as read
        await RefreshCountAsync();
    }

    private async Task HandleNotificationDismissed(Guid notificationId)
    {
        if (_disposed)
        {
            return;
        }

        // Refresh the count when a notification is dismissed
        await RefreshCountAsync();
    }
}
