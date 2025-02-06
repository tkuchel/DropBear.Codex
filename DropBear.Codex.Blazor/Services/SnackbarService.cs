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

/// <summary>
///     Provides methods to show, remove, and manage snackbars.
/// </summary>
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

    /// <summary>
    ///     Shows the provided snackbar. If the maximum number of snackbars is reached, it removes
    ///     the oldest non-error snackbar (if available) to make room.
    /// </summary>
    /// <param name="snackbar">The snackbar instance to show.</param>
    /// <returns>A result indicating success or a SnackbarError on failure.</returns>
    public async Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar)
    {
        if (_isDisposed)
        {
            return Result<Unit, SnackbarError>.Failure(new SnackbarError("Service is disposed."));
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Attempting to show snackbar {SnackbarId}. Current count: {Count}",
                snackbar.Id, _activeSnackbars.Count);

            if (_activeSnackbars.Count >= MaxSnackbars)
            {
                // Remove oldest non-error snackbar if available
                var oldestNonError = _activeSnackbars.Values
                    .Where(s => s.Type != SnackbarType.Error)
                    .OrderBy(s => s.CreatedAt)
                    .FirstOrDefault();

                if (oldestNonError != null)
                {
                    _logger.LogDebug("Removing oldest non-error snackbar {SnackbarId} to make room",
                        oldestNonError.Id);
                    await RemoveSnackbar(oldestNonError.Id).ConfigureAwait(false);
                }
                else if (snackbar.Type != SnackbarType.Error)
                {
                    _logger.LogWarning("Cannot show new snackbar - maximum error snackbars reached");
                    return Result<Unit, SnackbarError>.Failure(
                        new SnackbarError("Maximum number of error snackbars reached."));
                }
            }

            // Remove any existing instance with the same ID.
            if (_activeSnackbars.ContainsKey(snackbar.Id))
            {
                _logger.LogWarning("Snackbar {SnackbarId} already exists, removing old instance first", snackbar.Id);
                await RemoveSnackbar(snackbar.Id).ConfigureAwait(false);
            }

            if (_activeSnackbars.TryAdd(snackbar.Id, snackbar))
            {
                _logger.LogDebug("Successfully added snackbar {SnackbarId}. New count: {Count}",
                    snackbar.Id, _activeSnackbars.Count);

                if (OnShow != null)
                {
                    await OnShow.Invoke(snackbar).ConfigureAwait(false);
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

    /// <summary>
    ///     Removes a snackbar with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the snackbar to remove.</param>
    /// <returns>A result indicating success or a SnackbarError on failure.</returns>
    public async Task<Result<Unit, SnackbarError>> RemoveSnackbar(string id)
    {
        if (_isDisposed)
        {
            return Result<Unit, SnackbarError>.Failure(new SnackbarError("Service is disposed."));
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Attempting to remove snackbar {SnackbarId}. Current count: {Count}",
                id, _activeSnackbars.Count);

            if (_activeSnackbars.TryRemove(id, out var removed))
            {
                _logger.LogDebug("Successfully removed snackbar {SnackbarId}. New count: {Count}",
                    id, _activeSnackbars.Count);

                if (OnRemove != null)
                {
                    await OnRemove.Invoke(id).ConfigureAwait(false);
                }

                return Result<Unit, SnackbarError>.Success(Unit.Value);
            }

            _logger.LogWarning("Failed to remove snackbar {SnackbarId} - not found in active snackbars", id);
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

    /// <summary>
    ///     Returns a read-only collection of active snackbar instances.
    /// </summary>
    public IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars()
    {
        // Create a snapshot copy to avoid threading issues.
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
            await _semaphore.WaitAsync().ConfigureAwait(false);

            // Clear event handlers
            OnShow = null;
            OnRemove = null;

            // Remove all active snackbars
            var ids = _activeSnackbars.Keys.ToList();
            foreach (var id in ids)
            {
                await RemoveSnackbar(id).ConfigureAwait(false);
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
