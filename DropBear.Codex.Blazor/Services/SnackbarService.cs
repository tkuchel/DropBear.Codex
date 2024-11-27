#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public class SnackbarService : ISnackbarService
{
    private readonly List<SnackbarInstance> _activeSnackbars = new();
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SnackbarService()
    {
        _logger = LoggerFactory.Logger.ForContext<SnackbarService>();
    }

    public event Func<SnackbarInstance, Task>? OnShow;

    public async Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar)
    {
        try
        {
            await _semaphore.WaitAsync();

            if (OnShow is null)
            {
                return Result<Unit, SnackbarError>.Failure(
                    new SnackbarError("No snackbar container is registered"));
            }

            _activeSnackbars.Add(snackbar);
            await OnShow.Invoke(snackbar);

            return Result<Unit, SnackbarError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show snackbar");
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError($"Failed to show snackbar: {ex.Message}"));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Result<Unit, SnackbarError>> ShowSuccess(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Success,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    public async Task<Result<Unit, SnackbarError>> ShowError(
        string title,
        string message,
        int duration = 0,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Error,
            Duration = duration,
            RequiresManualClose = duration == 0,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    public async Task<Result<Unit, SnackbarError>> ShowWarning(
        string title,
        string message,
        int duration = 8000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Warning,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    public async Task<Result<Unit, SnackbarError>> ShowInformation(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Information,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        });
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
