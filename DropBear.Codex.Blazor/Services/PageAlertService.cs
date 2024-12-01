#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Services;

public sealed class PageAlertService : IPageAlertService
{
    public event Func<PageAlertInstance, Task>? OnAlert;
    public event Action? OnClear;

    public void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info, int? duration = 5000,
        bool isPermanent = false)
    {
        var alert = new PageAlertInstance
        {
            Id = Guid.NewGuid().ToString("N"), // Remove the alert- prefix
            Title = title,
            Message = message,
            Type = type,
            Duration = duration,
            IsPermanent = isPermanent
        };

        OnAlert?.Invoke(alert);
    }

    public void ShowSuccess(string title, string message, int? duration = 5000)
    {
        ShowAlert(title, message, PageAlertType.Success, duration);
    }

    public void ShowError(string title, string message, int? duration = 8000)
    {
        ShowAlert(title, message, PageAlertType.Error, duration);
    }

    public void ShowWarning(string title, string message, bool isPermanent = false)
    {
        ShowAlert(title, message, PageAlertType.Warning, null, isPermanent);
    }

    public void ShowInfo(string title, string message, bool isPermanent = false)
    {
        ShowAlert(title, message, PageAlertType.Info, null, isPermanent);
    }

    public void Clear()
    {
        OnClear?.Invoke();
    }
}
