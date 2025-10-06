#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class NotificationCenter : DropBearComponentBase
{
    private readonly CancellationTokenSource _componentCts = new();
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
    private Codex.Notifications.Filters.NotificationFilter _filter = new();
    private bool _initialized;
    private bool _loading = true;
    private List<NotificationRecord> _notifications = new();
    private bool _showFilterPanel;
    private int _totalCount;
    [Inject] private INotificationCenterService NotificationService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter] public Guid UserId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Set up initial filter
        _filter = new Codex.Notifications.Filters.NotificationFilter
        {
            UserId = UserId,
            IsRead = false,
            IsDismissed = false,
            PageNumber = 1,
            PageSize = 10,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        // Subscribe to events
        NotificationService.OnNotificationReceived += HandleNotificationReceived;
        NotificationService.OnNotificationRead += HandleNotificationReadEvent;
        NotificationService.OnNotificationDismissed += HandleNotificationDismissedEvent;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadNotificationsAsync();
            _initialized = true;
        }
    }

    private async Task LoadNotificationsAsync()
    {
        if (UserId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _loadingSemaphore.WaitAsync();
            _loading = true;
            StateHasChanged();

            var result = await NotificationService.GetNotificationsAsync(
                _filter,
                _componentCts.Token);

            _notifications = new List<NotificationRecord>(result.Notifications);
            _totalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            LogError("Failed to load notifications", ex);
        }
        finally
        {
            _loading = false;
            _loadingSemaphore.Release();
            StateHasChanged();
        }
    }

    private async Task HandleFilterChanged(Codex.Notifications.Filters.NotificationFilter filter)
    {
        _filter = filter;
        _showFilterPanel = false;
        await LoadNotificationsAsync();
    }

    private void ToggleFilterPanel()
    {
        _showFilterPanel = !_showFilterPanel;
        StateHasChanged();
    }

    private async Task HandlePageChange(int page)
    {
        if (page == _filter.PageNumber)
        {
            return;
        }

        _filter.PageNumber = page;
        await LoadNotificationsAsync();
    }

    private async Task HandleMarkAsRead(Guid notificationId)
    {
        try
        {
            await NotificationService.MarkAsReadAsync(notificationId);

            // Update the local collection
            var notification = _notifications.Find(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.ReadAt = DateTime.UtcNow;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to mark notification {notificationId} as read", ex);
        }
    }

    private async Task HandleDismiss(Guid notificationId)
    {
        try
        {
            await NotificationService.DismissAsync(notificationId);

            // Remove from local collection if showing non-dismissed only
            if (_filter.IsDismissed.HasValue && !_filter.IsDismissed.Value)
            {
                _notifications.RemoveAll(n => n.Id == notificationId);
                _totalCount--;
                StateHasChanged();
            }
            else
            {
                // Otherwise just update the record
                var notification = _notifications.Find(n => n.Id == notificationId);
                if (notification != null)
                {
                    notification.DismissedAt = DateTime.UtcNow;
                    StateHasChanged();
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to dismiss notification {notificationId}", ex);
        }
    }

    private async Task HandleMarkAllAsRead()
    {
        try
        {
            await NotificationService.MarkAllAsReadAsync(UserId);

            // Update local records
            foreach (var notification in _notifications)
            {
                if (notification.ReadAt == null)
                {
                    notification.ReadAt = DateTime.UtcNow;
                }
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            LogError("Failed to mark all notifications as read", ex);
        }
    }

    private async Task RefreshNotifications()
    {
        await LoadNotificationsAsync();
    }

    // Event handlers
    private async Task HandleNotificationReceived(NotificationRecord notification)
    {
        if (!_initialized || IsDisposed)
        {
            return;
        }

        if (notification.UserId != UserId)
        {
            return;
        }

        // Check if this notification meets current filter criteria
        if (ShouldShowNotification(notification))
        {
            // Add to the top of the list
            _notifications.Insert(0, notification);
            _totalCount++;

            // If we're beyond page size, remove the last item
            if (_notifications.Count > _filter.PageSize)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleNotificationReadEvent(Guid notificationId)
    {
        if (!_initialized || IsDisposed)
        {
            return;
        }

        var notification = _notifications.Find(n => n.Id == notificationId);
        if (notification != null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleNotificationDismissedEvent(Guid notificationId)
    {
        if (!_initialized || IsDisposed)
        {
            return;
        }

        // Remove from local collection if showing non-dismissed only
        if (_filter.IsDismissed.HasValue && !_filter.IsDismissed.Value)
        {
            _notifications.RemoveAll(n => n.Id == notificationId);
            _totalCount--;
        }
        else
        {
            // Otherwise just update the record
            var notification = _notifications.Find(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.DismissedAt = DateTime.UtcNow;
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private bool ShouldShowNotification(NotificationRecord notification)
    {
        // Apply current filter logic
        if (_filter.IsRead.HasValue && _filter.IsRead.Value != (notification.ReadAt != null))
        {
            return false;
        }

        if (_filter.IsDismissed.HasValue && _filter.IsDismissed.Value != (notification.DismissedAt != null))
        {
            return false;
        }

        if (_filter.Type.HasValue && notification.Type != _filter.Type.Value)
        {
            return false;
        }

        if (_filter.Severity.HasValue && notification.Severity != _filter.Severity.Value)
        {
            return false;
        }

        if (_filter.FromDate.HasValue && notification.CreatedAt < _filter.FromDate.Value)
        {
            return false;
        }

        if (_filter.ToDate.HasValue && notification.CreatedAt > _filter.ToDate.Value)
        {
            return false;
        }

        return true;
    }

    public override async ValueTask DisposeAsync()
    {
        // Clean up event handlers
        if (NotificationService != null)
        {
            NotificationService.OnNotificationReceived -= HandleNotificationReceived;
            NotificationService.OnNotificationRead -= HandleNotificationReadEvent;
            NotificationService.OnNotificationDismissed -= HandleNotificationDismissedEvent;
        }

        // Cancel any pending operations
        try
        {
            _componentCts.Cancel();
            _componentCts.Dispose();
            _loadingSemaphore.Dispose();
        }
        catch (Exception ex)
        {
            LogError("Error during NotificationCenter disposal", ex);
        }

        await base.DisposeAsync();
    }
}
