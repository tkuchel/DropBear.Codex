#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing modals in Blazor Server applications with optimized
///     queuing, memory usage, and proper error handling.
/// </summary>
public sealed class ModalService : IModalService, IAsyncDisposable
{
    #region Constructors

    /// <summary>
    ///     Creates a new instance of the <see cref="ModalService" /> class.
    /// </summary>
    /// <param name="logger">A logger instance for logging debug information.</param>
    public ModalService(ILogger<ModalService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("ModalService initialized");
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Represents an item in the modal queue with minimal memory footprint.
    /// </summary>
    private sealed class ModalQueueItem
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ModalQueueItem" /> class.
        /// </summary>
        /// <param name="componentType">The type of the component.</param>
        /// <param name="parameters">The parameters for the component.</param>
        public ModalQueueItem(Type componentType, IDictionary<string, object> parameters)
        {
            ComponentType = componentType;
            Parameters = parameters;
        }

        /// <summary>
        ///     Gets the type of the component to display.
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        ///     Gets the parameters for the component.
        /// </summary>
        public IDictionary<string, object> Parameters { get; }
    }

    #endregion

    #region Fields and Constants

    /// <summary>
    ///     Default timeout for modal operations in milliseconds.
    /// </summary>
    private const int DefaultOperationTimeoutMs = 5000;

    /// <summary>
    ///     Logger for diagnostic information.
    /// </summary>
    private readonly ILogger<ModalService> _logger;

    /// <summary>
    ///     Thread-safe queue to hold pending modals.
    /// </summary>
    private readonly ConcurrentQueue<ModalQueueItem> _modalQueue = new();

    /// <summary>
    ///     Semaphore to synchronize modal operations.
    /// </summary>
    private readonly SemaphoreSlim _modalSemaphore = new(1, 1);

    /// <summary>
    ///     Semaphore to control notification frequency.
    /// </summary>
    private readonly SemaphoreSlim _notificationSemaphore = new(1, 1);

    /// <summary>
    ///     Cancellation token source for shutdown.
    /// </summary>
    private readonly CancellationTokenSource _disposalCts = new();

    /// <summary>
    ///     Flag indicating whether a modal is visible.
    /// </summary>
    private volatile bool _isModalVisible;

    /// <summary>
    ///     Flag indicating whether the service is processing events.
    /// </summary>
    private volatile bool _isProcessingEvents;

    /// <summary>
    ///     Counter for pending notifications to prevent event storms.
    /// </summary>
    private int _pendingNotifications;

    /// <summary>
    ///     Flag to track disposal state.
    /// </summary>
    private bool _isDisposed;

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets the current component being displayed in the modal, if any.
    /// </summary>
    public Type? CurrentComponent { get; private set; }

    /// <summary>
    ///     Gets the parameters associated with the current component being displayed in the modal.
    /// </summary>
    public IDictionary<string, object>? CurrentParameters { get; private set; }

    /// <summary>
    ///     Indicates whether a modal is currently visible.
    /// </summary>
    public bool IsModalVisible => _isModalVisible;

    /// <summary>
    ///     Gets the number of modals currently in the queue.
    /// </summary>
    public int QueueCount => _modalQueue.Count;

    /// <summary>
    ///     Event triggered when the modal state changes, used to notify subscribers.
    /// </summary>
    public event Action? OnChange;

    #endregion

    #region Legacy Synchronous Methods

    /// <summary>
    ///     Displays the specified component as a modal with optional parameters.
    ///     This is a backwards-compatibility implementation that wraps the async version.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, ModalError> Show<T>(IDictionary<string, object>? parameters = null)
        where T : DropBearComponentBase
    {
        // Fire and forget the async operation
        _ = ShowAsync<T>(parameters)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger.LogError(task.Exception, "Error in Show<T> fire-and-forget for {ComponentType}",
                        typeof(T).Name);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Displays the specified component as a modal with a single parameter.
    ///     This is a backwards-compatibility implementation that wraps the async version.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameterName">Name of the parameter to pass.</param>
    /// <param name="parameterValue">Value of the parameter to pass.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, ModalError> Show<T>(string parameterName, object parameterValue) where T : DropBearComponentBase
    {
        // Fire and forget the async operation
        _ = ShowAsync<T>(parameterName, parameterValue)
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger.LogError(task.Exception,
                        "Error in Show<T>(string, object) fire-and-forget for {ComponentType} with parameter {ParameterName}",
                        typeof(T).Name, parameterName);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Closes the currently displayed modal and displays the next modal in the queue.
    ///     This is a backwards-compatibility implementation that wraps the async version.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, ModalError> Close()
    {
        // Fire and forget the async operation
        _ = CloseAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger.LogError(task.Exception, "Error in Close fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Clears all modals from the queue and closes any currently displayed modal.
    ///     This is a backwards-compatibility implementation that wraps the async version.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, ModalError> ClearAll()
    {
        // Fire and forget the async operation
        _ = ClearAllAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger.LogError(task.Exception, "Error in ClearAll fire-and-forget");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    #endregion

    #region Async Methods

    /// <summary>
    ///     Displays the specified component as a modal, with optional parameters.
    ///     If another modal is already visible, the new modal will be added to a queue and displayed
    ///     when the current modal is closed.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, ModalError>> ShowAsync<T>(
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
        where T : DropBearComponentBase
    {
        ThrowIfDisposed();

        parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
        var componentType = typeof(T);

        _logger.LogDebug("ShowAsync called for modal of type {ComponentType}", componentType.Name);

        try
        {
            // Create linked token with disposal token and timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposalCts.Token,
                cancellationToken);

            // Use the extension method to add timeout
            var timeoutTask = Task.Delay(DefaultOperationTimeoutMs, linkedCts.Token);

            // Try to acquire the semaphore with timeout
            var semaphoreTask = _modalSemaphore.WaitAsync(linkedCts.Token);

            // Wait for either the semaphore or timeout
            var completedTask = await Task.WhenAny(semaphoreTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to show {ComponentType}",
                    componentType.Name);
                return Result<Unit, ModalError>.Failure(
                    ModalError.ShowFailed(componentType.Name, "Timed out waiting for modal lock")
                );
            }

            // Successfully acquired the semaphore
            try
            {
                if (_isModalVisible)
                {
                    // Another modal is active; enqueue this one
                    _logger.LogDebug("Modal of type {ComponentType} is enqueued (another is displayed)",
                        componentType.Name);

                    // Create a copy of parameters to avoid modification while queued
                    var parametersCopy = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
                    _modalQueue.Enqueue(new ModalQueueItem(componentType, parametersCopy));
                }
                else
                {
                    // No active modal, display immediately
                    _logger.LogDebug("No modal currently displayed; showing modal of type {ComponentType}",
                        componentType.Name);

                    CurrentComponent = componentType;
                    CurrentParameters = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
                    _isModalVisible = true;

                    // Trigger notification
                    await NotifyStateChangedAsync(linkedCts.Token).ConfigureAwait(false);
                }

                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Show operation for {ComponentType} was cancelled", componentType.Name);
            return Result<Unit, ModalError>.Failure(
                ModalError.ShowFailed(componentType.Name, "Operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing modal of type {ComponentType}", componentType.Name);
            return Result<Unit, ModalError>.Failure(
                ModalError.ShowFailed(componentType.Name, $"Error showing modal: {ex.Message}"),
                ex
            );
        }
    }

    /// <summary>
    ///     Displays the specified component as a modal, with a single parameter.
    ///     Convenience method for simple cases.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameterName">Name of the parameter to pass.</param>
    /// <param name="parameterValue">Value of the parameter to pass.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public Task<Result<Unit, ModalError>> ShowAsync<T>(
        string parameterName,
        object parameterValue,
        CancellationToken cancellationToken = default)
        where T : DropBearComponentBase
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return Task.FromResult(Result<Unit, ModalError>.Failure(
                ModalError.ShowFailed(typeof(T).Name, "Parameter name cannot be null or empty")
            ));
        }

        var parameters = new Dictionary<string, object>(StringComparer.Ordinal) { { parameterName, parameterValue } };
        return ShowAsync<T>(parameters, cancellationToken);
    }

    /// <summary>
    ///     Closes the currently displayed modal and displays the next modal in the queue.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, ModalError>> CloseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Create linked token with disposal token and timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposalCts.Token,
                cancellationToken);

            // Use the extension method to add timeout
            var timeoutTask = Task.Delay(DefaultOperationTimeoutMs, linkedCts.Token);

            // Try to acquire the semaphore with timeout
            var semaphoreTask = _modalSemaphore.WaitAsync(linkedCts.Token);

            // Wait for either the semaphore or timeout
            var completedTask = await Task.WhenAny(semaphoreTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to close modal");
                return Result<Unit, ModalError>.Failure(
                    ModalError.CloseFailed("Timed out waiting for modal lock")
                );
            }

            try
            {
                _logger.LogDebug("Closing the current modal");

                // Store the component type for logging
                var closedComponentType = CurrentComponent?.Name ?? "Unknown";

                // Clear current modal
                CurrentComponent = null;
                CurrentParameters = null;
                _isModalVisible = false;

                // Check if we have a queued modal
                if (_modalQueue.TryDequeue(out var nextModal))
                {
                    _logger.LogDebug("Displaying next modal from queue: type {ComponentType}",
                        nextModal.ComponentType.Name);

                    CurrentComponent = nextModal.ComponentType;
                    CurrentParameters = nextModal.Parameters;
                    _isModalVisible = true;
                }
                else
                {
                    _logger.LogDebug("No modals left in the queue");
                }

                // Trigger UI update with debounce protection
                await NotifyStateChangedAsync(linkedCts.Token).ConfigureAwait(false);

                _logger.LogDebug("Successfully closed modal: {ClosedComponentType}", closedComponentType);
                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Close operation was cancelled");
            return Result<Unit, ModalError>.Failure(
                ModalError.CloseFailed("Operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing modal");
            return Result<Unit, ModalError>.Failure(
                ModalError.CloseFailed($"Error closing modal: {ex.Message}"),
                ex
            );
        }
    }

    /// <summary>
    ///     Clears all modals from the queue and closes any currently displayed modal.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, ModalError>> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Create linked token with disposal token and timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposalCts.Token,
                cancellationToken);

            // Use the extension method to add timeout
            var timeoutTask = Task.Delay(DefaultOperationTimeoutMs, linkedCts.Token);

            // Try to acquire the semaphore with timeout
            var semaphoreTask = _modalSemaphore.WaitAsync(linkedCts.Token);

            // Wait for either the semaphore or timeout
            var completedTask = await Task.WhenAny(semaphoreTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to clear modals");
                return Result<Unit, ModalError>.Failure(
                    ModalError.QueueFailed("Timed out waiting for modal lock")
                );
            }

            try
            {
                _logger.LogDebug("Clearing all modals. Queue size: {QueueSize}", _modalQueue.Count);

                // Track queue item count for metrics
                var queueItemCount = 0;

                // Clear the queue first
                while (_modalQueue.TryDequeue(out _))
                {
                    queueItemCount++;
                }

                // Close current modal if any
                var hadVisibleModal = _isModalVisible;
                if (_isModalVisible)
                {
                    CurrentComponent = null;
                    CurrentParameters = null;
                    _isModalVisible = false;

                    // Trigger UI update
                    await NotifyStateChangedAsync(linkedCts.Token).ConfigureAwait(false);
                }

                _logger.LogDebug("Successfully cleared {QueueCount} queued modals and {VisibleCount} visible modal",
                    queueItemCount, hadVisibleModal ? 1 : 0);

                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ClearAll operation was cancelled");
            return Result<Unit, ModalError>.Failure(
                ModalError.QueueFailed("Operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all modals");
            return Result<Unit, ModalError>.Failure(
                ModalError.QueueFailed($"Error clearing modals: {ex.Message}"),
                ex
            );
        }
    }

    #endregion

    #region Resource Management

    /// <summary>
    ///     Gets the service health metrics.
    /// </summary>
    /// <returns>A dictionary of metrics for monitoring.</returns>
    public Result<IDictionary<string, object>, ModalError> GetServiceMetrics()
    {
        ThrowIfDisposed();

        try
        {
            var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["QueueSize"] = _modalQueue.Count,
                ["IsModalVisible"] = _isModalVisible,
                ["CurrentComponentType"] = CurrentComponent?.FullName ?? "None",
                ["IsProcessingEvents"] = _isProcessingEvents,
                ["PendingNotifications"] = _pendingNotifications,
                ["IsDisposed"] = _isDisposed
            };

            return Result<IDictionary<string, object>, ModalError>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service metrics");
            return Result<IDictionary<string, object>, ModalError>.Failure(
                new ModalError($"Error getting service metrics: {ex.Message}"),
                ex
            );
        }
    }

    /// <summary>
    ///     Disposes the service resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _logger.LogDebug("Disposing modal service");

            // Cancel any ongoing operations
            await _disposalCts.CancelAsync().ConfigureAwait(false);

            // Acquire the lock to prevent any new operations
            if (await _modalSemaphore.WaitAsync(1000).ConfigureAwait(false))
            {
                try
                {
                    // Clean up the modal queue to prevent memory leaks
                    while (_modalQueue.TryDequeue(out _)) { }

                    // Clean up parameters
                    CurrentComponent = null;
                    CurrentParameters = null;
                    _isModalVisible = false;
                }
                finally
                {
                    _modalSemaphore.Release();
                }
            }

            // Dispose semaphores
            _modalSemaphore.Dispose();
            _notificationSemaphore.Dispose();
            _disposalCts.Dispose();

            _logger.LogDebug("Modal service disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing modal service");
        }
    }

    /// <summary>
    ///     Disposes the service resources synchronously (legacy method).
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _logger.LogDebug("Synchronously disposing modal service");

            // Clean up the modal queue
            while (_modalQueue.TryDequeue(out _)) { }

            // Clean up parameters
            CurrentComponent = null;
            CurrentParameters = null;
            _isModalVisible = false;

            // Dispose semaphores and CTS
            _modalSemaphore.Dispose();
            _notificationSemaphore.Dispose();

            try
            {
                // Try to cancel but don't await the task
                _disposalCts.Cancel();
                _disposalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling operations during synchronous disposal");
            }

            _logger.LogDebug("Modal service synchronously disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing modal service synchronously");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Invokes the <see cref="OnChange" /> event to notify subscribers of the modal state update,
    ///     with debounce protection to prevent multiple rapid notifications.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    private async Task NotifyStateChangedAsync(CancellationToken cancellationToken = default)
    {
        // Skip if already processing or no event handler
        if (_isProcessingEvents || OnChange == null)
        {
            // Increment pending count to signal more changes happened
            Interlocked.Increment(ref _pendingNotifications);
            return;
        }

        try
        {
            // First quick check without waiting
            if (!await _notificationSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                // Increment pending count
                Interlocked.Increment(ref _pendingNotifications);
                return;
            }

            try
            {
                // Mark as processing to prevent recursive calls
                _isProcessingEvents = true;

                do
                {
                    // Reset pending notifications before invoking
                    Interlocked.Exchange(ref _pendingNotifications, 0);

                    // Invoke the event
                    OnChange?.Invoke();

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Modal state changed notification triggered");
                    }

                    // If more notifications arrived during processing, process them too
                } while (Interlocked.CompareExchange(ref _pendingNotifications, 0, 0) > 0);
            }
            finally
            {
                _isProcessingEvents = false;
                _notificationSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            _logger.LogDebug("Modal state notification was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while notifying modal state change");
        }
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the service is disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ModalService));
        }
    }

    #endregion
}
