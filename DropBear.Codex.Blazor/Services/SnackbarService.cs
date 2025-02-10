using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing snackbar notifications in Blazor Server.
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    private const int MaxSnackbars = 5;
    private const int DefaultSuccessDuration = 5000;
    private const int DefaultWarningDuration = 8000;
    private const int DefaultInfoDuration = 5000;
    private const int OperationTimeoutMs = 5000;

    private readonly ConcurrentDictionary<string, SnackbarInstance> _activeSnackbars;
    private readonly SemaphoreSlim _operationLock;
    private readonly ILogger<SnackbarService> _logger;
    private readonly CancellationTokenSource _disposalCts;
    private  int _isDisposed;

    public SnackbarService(ILogger<SnackbarService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeSnackbars = new ConcurrentDictionary<string, SnackbarInstance>();
        _operationLock = new SemaphoreSlim(1, 1);
        _disposalCts = new CancellationTokenSource();
    }

    public event Func<SnackbarInstance, Task>? OnShow;
    public event Func<string, Task>? OnRemove;

    public async Task<Result<Unit, SnackbarError>> Show(
        SnackbarInstance snackbar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );
        cts.CancelAfter(OperationTimeoutMs);

        try
        {
            await _operationLock.WaitAsync(cts.Token);
            try
            {
                await ManageSnackbarLimitAsync(snackbar.Type, cts.Token);

                if (_activeSnackbars.TryRemove(snackbar.Id, out _))
                {
                    _logger.LogDebug(
                        "Replaced existing snackbar: {Id}",
                        snackbar.Id
                    );
                }

                if (!_activeSnackbars.TryAdd(snackbar.Id, snackbar))
                {
                    throw new SnackbarException(
                        $"Failed to add snackbar: {snackbar.Id}"
                    );
                }

                await NotifySnackbarShownAsync(snackbar, cts.Token);
                return Result<Unit, SnackbarError>.Success(Unit.Value);
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError("Operation timed out")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show snackbar: {Id}", snackbar.Id);
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError(ex.Message)
            );
        }
    }

    public async Task<Result<Unit, SnackbarError>> RemoveSnackbar(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );
        cts.CancelAfter(OperationTimeoutMs);

        try
        {
            await _operationLock.WaitAsync(cts.Token);
            try
            {
                if (!_activeSnackbars.TryRemove(id, out _))
                {
                    return Result<Unit, SnackbarError>.Failure(
                        new SnackbarError($"Snackbar not found: {id}")
                    );
                }

                await NotifySnackbarRemovedAsync(id, cts.Token);
                return Result<Unit, SnackbarError>.Success(Unit.Value);
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError("Operation timed out")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove snackbar: {Id}", id);
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError(ex.Message)
            );
        }
    }

    public IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars()
    {
        ThrowIfDisposed();
        return _activeSnackbars.Values.ToList().AsReadOnly();
    }

    public Task<Result<Unit, SnackbarError>> ShowSuccess(
        string title,
        string message,
        int duration = DefaultSuccessDuration,
        CancellationToken cancellationToken = default)
    {
        return Show(
            CreateSnackbar(title, message, SnackbarType.Success, duration),
            cancellationToken
        );
    }

    public Task<Result<Unit, SnackbarError>> ShowError(
        string title,
        string message,
        int duration = 0,
        CancellationToken cancellationToken = default)
    {
        return Show(
            CreateSnackbar(title, message, SnackbarType.Error, duration, true),
            cancellationToken
        );
    }

    public Task<Result<Unit, SnackbarError>> ShowWarning(
        string title,
        string message,
        int duration = DefaultWarningDuration,
        CancellationToken cancellationToken = default)
    {
        return Show(
            CreateSnackbar(title, message, SnackbarType.Warning, duration),
            cancellationToken
        );
    }

    public Task<Result<Unit, SnackbarError>> ShowInformation(
        string title,
        string message,
        int duration = DefaultInfoDuration,
        CancellationToken cancellationToken = default)
    {
        return Show(
            CreateSnackbar(title, message, SnackbarType.Information, duration),
            cancellationToken
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        try
        {
            await _disposalCts.CancelAsync();

            await _operationLock.WaitAsync();
            try
            {
                foreach (var id in _activeSnackbars.Keys.ToList())
                {
                    try
                    {
                        await RemoveSnackbar(
                            id,
                            new CancellationTokenSource(1000).Token
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error removing snackbar during disposal: {Id}",
                            id
                        );
                    }
                }

                _activeSnackbars.Clear();
            }
            finally
            {
                _operationLock.Release();
            }
        }
        finally
        {
            _disposalCts.Dispose();
            _operationLock.Dispose();
        }
    }

    private async Task ManageSnackbarLimitAsync(
        SnackbarType newType,
        CancellationToken cancellationToken)
    {
        if (_activeSnackbars.Count < MaxSnackbars)
            return;

        if (newType != SnackbarType.Error)
        {
            var oldest = _activeSnackbars.Values
                .Where(s => s.Type != SnackbarType.Error)
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefault();

            if (oldest != null)
            {
                await RemoveSnackbar(oldest.Id, cancellationToken);
                return;
            }
        }

        if (newType != SnackbarType.Error)
        {
            throw new SnackbarException("Maximum error snackbars reached");
        }
    }

    private async Task NotifySnackbarShownAsync(
        SnackbarInstance snackbar,
        CancellationToken cancellationToken)
    {
        if (OnShow != null)
        {
            try
            {
                await OnShow.Invoke(snackbar);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in OnShow handler for: {Id}",
                    snackbar.Id
                );
            }
        }
    }

    private async Task NotifySnackbarRemovedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (OnRemove != null)
        {
            try
            {
                await OnRemove.Invoke(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in OnRemove handler for: {Id}",
                    id
                );
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SnackbarInstance CreateSnackbar(
        string title,
        string message,
        SnackbarType type,
        int duration,
        bool requiresManualClose = false)
    {
        return new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = type,
            Duration = duration,
            RequiresManualClose = requiresManualClose,
            CreatedAt = DateTime.UtcNow
        };
    }

    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed SnackbarService"
            );
        }
    }
}
