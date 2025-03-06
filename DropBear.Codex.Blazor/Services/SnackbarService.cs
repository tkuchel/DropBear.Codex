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
///     Thread-safe service for managing snackbar notifications in Blazor Server
///     with optimized memory usage, performance, and proper error handling.
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    #region Fields and Constants

    /// <summary>
    /// Maximum number of snackbars to display simultaneously.
    /// </summary>
    private const int MaxSnackbars = 5;

    /// <summary>
    /// Default duration for success snackbars in milliseconds.
    /// </summary>
    private const int DefaultSuccessDuration = 5000;

    /// <summary>
    /// Default duration for warning snackbars in milliseconds.
    /// </summary>
    private const int DefaultWarningDuration = 8000;

    /// <summary>
    /// Default duration for information snackbars in milliseconds.
    /// </summary>
    private const int DefaultInfoDuration = 5000;

    /// <summary>
    /// Default timeout for operations in milliseconds.
    /// </summary>
    private const int OperationTimeoutMs = 5000;

    /// <summary>
    /// Default interval for cleanup timer in minutes.
    /// </summary>
    private const int CleanupIntervalMinutes = 5;

    /// <summary>
    /// Default age limit for stale snackbars in hours.
    /// </summary>
    private const int StaleSnackbarAgeHours = 1;

    /// <summary>
    /// Storage for active snackbars with thread safety.
    /// </summary>
    private readonly ConcurrentDictionary<string, SnackbarInstance> _activeSnackbars;

    /// <summary>
    /// Cancellation token source for shutdown control.
    /// </summary>
    private readonly CancellationTokenSource _disposalCts;

    /// <summary>
    /// Logger for diagnostic information.
    /// </summary>
    private readonly ILogger<SnackbarService> _logger;

    /// <summary>
    /// Lock for synchronizing operations.
    /// </summary>
    private readonly SemaphoreSlim _operationLock;

    /// <summary>
    /// Timer for cleaning up stale snackbars.
    /// </summary>
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Flag indicating disposal state.
    /// </summary>
    private int _isDisposed;

    #endregion

    #region Events

    /// <summary>
    ///     Occurs when a snackbar is shown.
    /// </summary>
    public event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    ///     Occurs when a snackbar is removed.
    /// </summary>
    public event Func<string, Task>? OnRemove;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarService"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public SnackbarService(ILogger<SnackbarService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeSnackbars = new ConcurrentDictionary<string, SnackbarInstance>();
        _operationLock = new SemaphoreSlim(1, 1);
        _disposalCts = new CancellationTokenSource();

        // Setup cleanup timer
        _cleanupTimer = new Timer(
            CleanupStaleSnackbars,
            null,
            TimeSpan.FromMinutes(CleanupIntervalMinutes),
            TimeSpan.FromMinutes(CleanupIntervalMinutes));

        _logger.LogDebug("SnackbarService initialized");
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Shows a snackbar with thread-safe state management.
    /// </summary>
    /// <param name="snackbar">The snackbar to show.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    public async Task<Result<Unit, SnackbarError>> Show(
        SnackbarInstance snackbar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ThrowIfDisposed();

        // Create linked token with disposal token
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );
        cts.CancelAfter(OperationTimeoutMs);

        try
        {
            // Ensure ID is set
            // if (string.IsNullOrEmpty(snackbar.Id))
            // {
            //     snackbar.Id = $"snackbar-{Guid.NewGuid():N}";
            // }

            // Ensure CreatedAt is set
            // if (snackbar.CreatedAt == default)
            // {
            //     snackbar.CreatedAt = DateTime.UtcNow;
            // }

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

    /// <summary>
    ///     Shows multiple snackbars in a batch operation.
    /// </summary>
    /// <param name="snackbars">The collection of snackbars to show.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result indicating success, partial success, or failure.</returns>
    public async Task<Result<Unit, SnackbarError>> ShowBatchAsync(
        IEnumerable<SnackbarInstance> snackbars,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snackbars);
        ThrowIfDisposed();

        var snackbarsList = snackbars.ToList();
        if (snackbarsList.Count == 0)
        {
            return Result<Unit, SnackbarError>.Success(Unit.Value);
        }

        var errors = new List<SnackbarError>();
        var successCount = 0;

        foreach (var snackbar in snackbarsList)
        {
            var result = await Show(snackbar, cancellationToken);
            if (result.IsSuccess)
            {
                successCount++;
            }
            else if (result.Error != null)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count == 0)
        {
            return Result<Unit, SnackbarError>.Success(Unit.Value);
        }

        var errorMessage = $"{errors.Count} snackbars failed to show: {string.Join("; ", errors.Select(e => e.Message))}";

        if (successCount > 0)
        {
            // Some succeeded, some failed
            return Result<Unit, SnackbarError>.PartialSuccess(
                Unit.Value,
                new SnackbarError(errorMessage));
        }

        // All failed
        return Result<Unit, SnackbarError>.Failure(
            new SnackbarError(errorMessage));
    }

    /// <summary>
    ///     Removes a snackbar by ID.
    /// </summary>
    /// <param name="id">The snackbar ID to remove.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
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

    /// <summary>
    ///     Gets a thread-safe snapshot of active snackbars.
    /// </summary>
    /// <returns>Read-only collection of active snackbars.</returns>
    public IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars()
    {
        ThrowIfDisposed();
        return _activeSnackbars.Values.ToList().AsReadOnly();
    }

    /// <summary>
    ///     Shows a success snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
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

    /// <summary>
    ///     Shows an error snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration (0 for manual close).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
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

    /// <summary>
    ///     Shows a warning snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
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

    /// <summary>
    ///     Shows an information snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
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

    /// <summary>
    ///     Clears all snackbars.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    public async Task<Result<Unit, SnackbarError>> ClearAllAsync(
        CancellationToken cancellationToken = default)
    {
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
                var snackbarIds = _activeSnackbars.Keys.ToList();
                _activeSnackbars.Clear();

                // Notify for each removed snackbar
                foreach (var id in snackbarIds)
                {
                    await NotifySnackbarRemovedAsync(id, cts.Token);
                }

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
            _logger.LogError(ex, "Failed to clear all snackbars");
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError($"Failed to clear all snackbars: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Gets the current service metrics for monitoring.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing a dictionary of metrics.</returns>
    public async Task<Result<IDictionary<string, object>, SnackbarError>> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                var metrics = new Dictionary<string, object>
                {
                    ["ActiveSnackbarCount"] = _activeSnackbars.Count,
                    ["MaxSnackbars"] = MaxSnackbars,
                    ["HasShowSubscribers"] = OnShow != null,
                    ["HasRemoveSubscribers"] = OnRemove != null,
                    ["IsDisposed"] = Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1,
                    ["OldestSnackbarAge"] = GetOldestSnackbarAge(),
                    ["SnackbarTypeBreakdown"] = GetSnackbarTypeBreakdown(),
                    ["Timestamp"] = DateTime.UtcNow
                };

                return Result<IDictionary<string, object>, SnackbarError>.Success(metrics);
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<IDictionary<string, object>, SnackbarError>.Failure(
                new SnackbarError("Operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics");
            return Result<IDictionary<string, object>, SnackbarError>.Failure(
                new SnackbarError($"Failed to get metrics: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Asynchronously disposes the service.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Disposing SnackbarService");

            // Cancel any ongoing operations
            await _disposalCts.CancelAsync();

            // Stop the cleanup timer
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);

            await _operationLock.WaitAsync(1000);
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

            // Dispose resources
            _cleanupTimer.Dispose();
            _operationLock.Dispose();
            _disposalCts.Dispose();

            _logger.LogDebug("SnackbarService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing SnackbarService");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles the cleanup of stale snackbars.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private async void CleanupStaleSnackbars(object? state)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1)
            {
                return;
            }

            // Only keep snackbars created within the last hour
            var cutoffTime = DateTime.UtcNow.AddHours(-StaleSnackbarAgeHours);
            var staleSnackbars = new List<string>();

            // Identify stale snackbars
            foreach (var (id, snackbar) in _activeSnackbars)
            {
                if (snackbar.CreatedAt < cutoffTime && !snackbar.RequiresManualClose)
                {
                    staleSnackbars.Add(id);
                }
            }

            // Log cleanup information
            if (staleSnackbars.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Cleaning up {Count} stale snackbars", staleSnackbars.Count);
            }

            // Remove stale snackbars
            foreach (var id in staleSnackbars)
            {
                try
                {
                    await RemoveSnackbar(id, _disposalCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error removing stale snackbar: {Id}", id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during stale snackbar cleanup");
        }
    }

    /// <summary>
    /// Manages the limit of active snackbars by removing older ones if necessary.
    /// </summary>
    /// <param name="newType">The type of the new snackbar.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ManageSnackbarLimitAsync(
        SnackbarType newType,
        CancellationToken cancellationToken)
    {
        if (_activeSnackbars.Count < MaxSnackbars)
        {
            return;
        }

        // If adding a non-error snackbar, try to remove an older non-error first
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

        // If only errors remain and we're trying to add another error
        if (newType == SnackbarType.Error)
        {
            // Remove the oldest error
            var oldestError = _activeSnackbars.Values
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefault();

            if (oldestError != null)
            {
                await RemoveSnackbar(oldestError.Id, cancellationToken);
                return;
            }
        }

        // If we can't add our snackbar type, throw an exception
        if (_activeSnackbars.Count >= MaxSnackbars)
        {
            throw new SnackbarException(
                $"Maximum {(newType == SnackbarType.Error ? "error " : "")}snackbars reached");
        }
    }

    /// <summary>
    /// Notifies subscribers when a snackbar is shown.
    /// </summary>
    /// <param name="snackbar">The snackbar being shown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task NotifySnackbarShownAsync(
        SnackbarInstance snackbar,
        CancellationToken cancellationToken)
    {
        if (OnShow != null)
        {
            try
            {
                // Check the token before invoking the event
                cancellationToken.ThrowIfCancellationRequested();

                // Notify listeners that a snackbar has been shown
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

    /// <summary>
    /// Notifies subscribers when a snackbar is removed.
    /// </summary>
    /// <param name="id">The ID of the removed snackbar.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task NotifySnackbarRemovedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (OnRemove != null)
        {
            try
            {
                // Check the token before invoking the event
                cancellationToken.ThrowIfCancellationRequested();

                // Notify listeners that a snackbar has been removed
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

    /// <summary>
    /// Creates a snackbar instance with the specified parameters.
    /// </summary>
    /// <param name="title">Snackbar title.</param>
    /// <param name="message">Snackbar message.</param>
    /// <param name="type">Snackbar type.</param>
    /// <param name="duration">Duration in milliseconds.</param>
    /// <param name="requiresManualClose">Whether manual closing is required.</param>
    /// <returns>A new snackbar instance.</returns>
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

    /// <summary>
    /// Gets the age of the oldest snackbar for metrics.
    /// </summary>
    /// <returns>Age in seconds, or 0 if no snackbars exist.</returns>
    private double GetOldestSnackbarAge()
    {
        if (_activeSnackbars.IsEmpty)
        {
            return 0;
        }

        var oldestTime = _activeSnackbars.Values.Min(s => s.CreatedAt);
        return (DateTime.UtcNow - oldestTime).TotalSeconds;
    }

    /// <summary>
    /// Gets the breakdown of snackbars by type for metrics.
    /// </summary>
    /// <returns>Dictionary with counts by type.</returns>
    private Dictionary<string, int> GetSnackbarTypeBreakdown()
    {
        var breakdown = new Dictionary<string, int>();

        foreach (var type in Enum.GetValues<SnackbarType>())
        {
            breakdown[type.ToString()] = _activeSnackbars.Values.Count(s => s.Type == type);
        }

        return breakdown;
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the service is disposed.
    /// </summary>
    /// <param name="caller">Name of the calling method.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    #endregion
}

