#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for the Page Alert Service.
/// </summary>
public interface IPageAlertService
{
    event Func<PageAlertInstance, Task> OnAlert;
    event Action OnClear;

    void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info, int? duration = 5000,
        bool isPermanent = false);

    void ShowSuccess(string title, string message, int? duration = 5000);
    void ShowError(string title, string message, int? duration = 8000);
    void ShowWarning(string title, string message, bool isPermanent = false);
    void ShowInfo(string title, string message, bool isPermanent = false);
    void Clear();
}
