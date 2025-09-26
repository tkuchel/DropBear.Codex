#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe implementation of the snackbar service for Blazor Server applications.
/// </summary>
public sealed class SnackbarService : ISnackbarService, IDisposable
{
    #region Fields

    private readonly ConcurrentDictionary<string, SnackbarInstance> _activeSnackbars = new();
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly ILogger<SnackbarService> _logger;
    private volatile bool _disposed;

    #endregion

    #region Constructor

    public SnackbarService(ILogger<SnackbarService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Events

    /// <inheritdoc />
    public event Func<SnackbarInstance, Task> OnShow = delegate { return Task.CompletedTask; };

    /// <inheritdoc />
    public event Func<string, Task> OnRemove = delegate { return Task.CompletedTask; };

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task Show(SnackbarInstance snackbar, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snackbar);

        if (string.IsNullOrWhiteSpace(snackbar.Message))
        {
            throw new ArgumentException("Snackbar message cannot be empty.", nameof(snackbar));
        }

        try
        {
            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Add to active collection
                _activeSnackbars.TryAdd(snackbar.Id, snackbar);

                // Notify subscribers
                await OnShow.Invoke(snackbar);

                _logger.LogDebug("Snackbar shown: {Id} - {Type} - {Message}",
                    snackbar.Id, snackbar.Type, snackbar.Message);

                // Auto-remove if duration is set and not manual close
                if (!snackbar.RequiresManualClose && snackbar.Duration > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(snackbar.Duration, cancellationToken);
                            await RemoveSnackbar(snackbar.Id, cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            // Expected when cancelled
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error auto-removing snackbar: {Id}", snackbar.Id);
                        }
                    }, cancellationToken);
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to show snackbar: {Id}", snackbar.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public Task ShowInfo(string message, string? title = null, int duration = 5000,
        CancellationToken cancellationToken = default)
    {
        var snackbar = new SnackbarInstance
        {
            Message = message,
            Title = title,
            Type = SnackbarType.Information,
            Duration = duration,
            RequiresManualClose = duration <= 0
        };

        return Show(snackbar, cancellationToken);
    }

    /// <inheritdoc />
    public Task ShowSuccess(string message, string? title = null, int duration = 4000,
        CancellationToken cancellationToken = default)
    {
        var snackbar = new SnackbarInstance
        {
            Message = message,
            Title = title,
            Type = SnackbarType.Success,
            Duration = duration,
            RequiresManualClose = duration <= 0
        };

        return Show(snackbar, cancellationToken);
    }

    /// <inheritdoc />
    public Task ShowWarning(string message, string? title = null, int duration = 7000,
        CancellationToken cancellationToken = default)
    {
        var snackbar = new SnackbarInstance
        {
            Message = message,
            Title = title,
            Type = SnackbarType.Warning,
            Duration = duration,
            RequiresManualClose = duration <= 0
        };

        return Show(snackbar, cancellationToken);
    }

    /// <inheritdoc />
    public Task ShowError(string message, string? title = null, int duration = 0,
        CancellationToken cancellationToken = default)
    {
        var snackbar = new SnackbarInstance
        {
            Message = message,
            Title = title,
            Type = SnackbarType.Error,
            Duration = duration,
            RequiresManualClose = true // Errors always require manual close
        };

        return Show(snackbar, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveSnackbar(string snackbarId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(snackbarId);

        try
        {
            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_activeSnackbars.TryRemove(snackbarId, out var removedSnackbar))
                {
                    await OnRemove.Invoke(snackbarId);
                    _logger.LogDebug("Snackbar removed: {Id}", snackbarId);
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to remove snackbar: {Id}", snackbarId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAll(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var snackbarIds = _activeSnackbars.Keys.ToList();
                _activeSnackbars.Clear();

                // Notify for each removed snackbar
                foreach (var id in snackbarIds)
                {
                    await OnRemove.Invoke(id);
                }

                _logger.LogDebug("All snackbars removed: {Count}", snackbarIds.Count);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to remove all snackbars");
            throw;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SnackbarInstance> GetActiveSnackbars()
    {
        ThrowIfDisposed();
        return _activeSnackbars.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public int GetActiveCount()
    {
        ThrowIfDisposed();
        return _activeSnackbars.Count;
    }

    /// <inheritdoc />
    public bool IsActive(string snackbarId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(snackbarId);
        return _activeSnackbars.ContainsKey(snackbarId);
    }

    #endregion

    #region Disposal

    /// <summary>
    ///     Disposes the service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _operationSemaphore.Wait(TimeSpan.FromSeconds(5));
            try
            {
                _activeSnackbars.Clear();
                OnShow = delegate { return Task.CompletedTask; };
                OnRemove = delegate { return Task.CompletedTask; };
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SnackbarService disposal");
        }
        finally
        {
            _operationSemaphore.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SnackbarService));
        }
    }

    #endregion
}
