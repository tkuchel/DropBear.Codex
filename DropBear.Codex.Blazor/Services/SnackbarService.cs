#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

public class SnackbarService : ISnackbarService
{
    private const int MaxSnackbars = 5;
    private readonly ConcurrentDictionary<string, SnackbarInstance> _activeSnackbars = new();
    private readonly ILogger<SnackbarService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    public SnackbarService(ILogger<SnackbarService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return Result<Unit, SnackbarError>.Failure(new SnackbarError("Service is disposed."));
        }

        if (snackbar == null)
        {
            _logger.LogWarning("Attempted to show a null snackbar instance.");
            return Result<Unit, SnackbarError>.Failure(new SnackbarError("Snackbar instance cannot be null."));
        }

        try
        {
            await _semaphore.WaitAsync();

            if (_activeSnackbars.Count >= MaxSnackbars)
            {
                // Remove oldest non-error snackbar
                var oldestNonError = _activeSnackbars.Values
                    .Where(s => s.Type != SnackbarType.Error)
                    .OrderBy(s => s.CreatedAt)
                    .FirstOrDefault();

                if (oldestNonError != null)
                {
                    await RemoveSnackbar(oldestNonError.Id);
                }
                else if (snackbar.Type != SnackbarType.Error)
                {
                    return Result<Unit, SnackbarError>.Failure(
                        new SnackbarError("Maximum number of error snackbars reached."));
                }
            }

            if (_activeSnackbars.TryAdd(snackbar.Id, snackbar))
            {
                _logger.LogDebug("Showing snackbar {SnackbarId}", snackbar.Id);
                if (OnShow != null)
                {
                    await OnShow.Invoke(snackbar);
                }

                return Result<Unit, SnackbarError>.Success(Unit.Value);
            }

            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError($"Failed to add snackbar {snackbar.Id} to active snackbars."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show snackbar {SnackbarId}", snackbar.Id);
            return Result<Unit, SnackbarError>.Failure(new SnackbarError($"Failed to show snackbar: {ex.Message}"));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Result<Unit, SnackbarError>> RemoveSnackbar(string id)
    {
        if (_isDisposed)
        {
            return Result<Unit, SnackbarError>.Failure(new SnackbarError("Service is disposed."));
        }

        try
        {
            await _semaphore.WaitAsync();

            if (_activeSnackbars.TryRemove(id, out _))
            {
                _logger.LogDebug("Removed snackbar {SnackbarId}", id);
                if (OnRemove != null)
                {
                    await OnRemove.Invoke(id);
                }

                return Result<Unit, SnackbarError>.Success(Unit.Value);
            }

            return Result<Unit, SnackbarError>.Failure(new SnackbarError($"Snackbar {id} not found."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove snackbar {SnackbarId}", id);
            return Result<Unit, SnackbarError>.Failure(new SnackbarError($"Failed to remove snackbar: {ex.Message}"));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars()
    {
        return _activeSnackbars.Values.ToList().AsReadOnly();
    }

    public Task<Result<Unit, SnackbarError>> ShowSuccess(string title, string message, int duration = 5000)
    {
        var snackbar = new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Success,
            Duration = duration,
            CreatedAt = DateTime.UtcNow
        };
        return Show(snackbar);
    }

    public Task<Result<Unit, SnackbarError>> ShowError(string title, string message, int duration = 0)
    {
        var snackbar = new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Error,
            Duration = duration,
            RequiresManualClose = duration == 0,
            CreatedAt = DateTime.UtcNow
        };
        return Show(snackbar);
    }

    public Task<Result<Unit, SnackbarError>> ShowWarning(string title, string message, int duration = 8000)
    {
        var snackbar = new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Warning,
            Duration = duration,
            CreatedAt = DateTime.UtcNow
        };
        return Show(snackbar);
    }

    public Task<Result<Unit, SnackbarError>> ShowInformation(string title, string message, int duration = 5000)
    {
        var snackbar = new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Information,
            Duration = duration,
            CreatedAt = DateTime.UtcNow
        };
        return Show(snackbar);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            await _semaphore.WaitAsync();

            // Clear all event handlers
            OnShow = null;
            OnRemove = null;

            // Remove all active snackbars
            foreach (var id in _activeSnackbars.Keys.ToList())
            {
                await RemoveSnackbar(id);
            }

            _activeSnackbars.Clear();
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }

    public event Func<SnackbarInstance, Task>? OnShow;
    public event Func<string, Task>? OnRemove;
}
