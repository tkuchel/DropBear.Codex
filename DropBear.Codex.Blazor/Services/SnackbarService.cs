#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Services;

public class SnackbarService : ISnackbarService
{
    /// <inheritdoc />
    public event Func<SnackbarInstance, Task>? OnShow;

    /// <inheritdoc />
    public async Task Show(SnackbarInstance snackbar)
    {
        if (OnShow is not null)
        {
            await OnShow.Invoke(snackbar);
        }
    }

    /// <inheritdoc />
    public async Task ShowSuccess(string title, string message, int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Success,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    /// <inheritdoc />
    public async Task ShowError(string title, string message, int duration = 0, List<SnackbarAction>? actions = null)
    {
        await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Error,
            Duration = duration,
            RequiresManualClose = duration == 0,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    /// <inheritdoc />
    public async Task ShowWarning(string title, string message, int duration = 8000,
        List<SnackbarAction>? actions = null)
    {
        await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Warning,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    /// <inheritdoc />
    public async Task ShowInformation(string title, string message, int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Information,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }
}
