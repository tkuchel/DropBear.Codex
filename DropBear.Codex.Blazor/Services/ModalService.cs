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
///     Manages the display of modals in the application, ensuring only one modal
///     is visible at a time and queuing others if necessary.
///     Optimized for Blazor Server performance and thread safety.
/// </summary>
public sealed class ModalService : IModalService
{
    private readonly ILogger<ModalService> _logger;

    /// <summary>
    ///     Thread-safe queue to hold modals if another one is currently displayed.
    /// </summary>
    private readonly ConcurrentQueue<(Type ComponentType, IDictionary<string, object> Parameters)> _modalQueue = new();

    private readonly SemaphoreSlim _modalSemaphore = new(1, 1);

    // Notification debouncing
    private readonly SemaphoreSlim _notificationSemaphore = new(1, 1);

    // Modal state with volatile for thread safety
    private volatile bool _isModalVisible;
    private volatile bool _isProcessingEvents;
    private int _pendingNotifications;

    /// <summary>
    ///     Creates a new instance of the <see cref="ModalService" /> class.
    /// </summary>
    /// <param name="logger">A logger instance for logging debug information.</param>
    public ModalService(ILogger<ModalService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("ModalService initialized.");
    }

    /// <summary>
    ///     Gets the currently visible modal component type, or <c>null</c> if none.
    /// </summary>
    public Type? CurrentComponent { get; private set; }

    /// <summary>
    ///     Gets the parameters for the current modal component, if any.
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
    ///     Event fired whenever the modal state changes, allowing the UI to re-render.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    ///     Backwards compatibility implementation of Show
    /// </summary>
    public Result<Unit, ModalError> Show<T>(IDictionary<string, object>? parameters = null)
        where T : DropBearComponentBase
    {
        // Fire and forget the async operation
        var task = ShowAsync<T>(parameters);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Backwards compatibility implementation of Show with single parameter
    /// </summary>
    public Result<Unit, ModalError> Show<T>(string parameterName, object parameterValue) where T : DropBearComponentBase
    {
        // Fire and forget the async operation
        var task = ShowAsync<T>(parameterName, parameterValue);

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Backwards compatibility implementation of Close
    /// </summary>
    public Result<Unit, ModalError> Close()
    {
        // Fire and forget the async operation
        var task = CloseAsync();

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Backwards compatibility implementation of ClearAll
    /// </summary>
    public Result<Unit, ModalError> ClearAll()
    {
        // Fire and forget the async operation
        var task = ClearAllAsync();

        // Return success immediately since we can't wait
        return Result<Unit, ModalError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Shows a modal of type <typeparamref name="T" />. If another modal is already visible,
    ///     the new modal is added to a queue and displayed later.
    /// </summary>
    /// <typeparam name="T">A Blazor component derived from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the modal component.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public async Task<Result<Unit, ModalError>> ShowAsync<T>(IDictionary<string, object>? parameters = null)
        where T : DropBearComponentBase
    {
        parameters ??= new Dictionary<string, object>();
        var componentType = typeof(T);

        _logger.LogDebug("Show called for modal of type {ComponentType}.", componentType.Name);

        try
        {
            // Use non-blocking wait with timeout for better thread safety
            if (!await _modalSemaphore.WaitAsync(2000))
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to show {ComponentType}.",
                    componentType.Name);
                return Result<Unit, ModalError>.Failure(
                    ModalError.ShowFailed(componentType.Name, "Timed out waiting for modal lock")
                );
            }

            try
            {
                if (_isModalVisible)
                {
                    // Another modal is active; enqueue this one
                    _logger.LogDebug("Modal of type {ComponentType} is enqueued (another is displayed).",
                        componentType.Name);
                    _modalQueue.Enqueue((componentType, new Dictionary<string, object>(parameters)));
                }
                else
                {
                    // No active modal, display immediately
                    _logger.LogDebug("No modal currently displayed; showing modal of type {ComponentType}.",
                        componentType.Name);
                    CurrentComponent = componentType;
                    CurrentParameters = new Dictionary<string, object>(parameters);
                    _isModalVisible = true;

                    // Trigger notification
                    await NotifyStateChangedAsync();
                }

                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing modal of type {ComponentType}.", componentType.Name);
            return Result<Unit, ModalError>.Failure(
                ModalError.ShowFailed(componentType.Name, $"Error showing modal: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Shows a modal of type <typeparamref name="T" /> with a single parameter.
    ///     Convenience method for simple cases.
    /// </summary>
    /// <typeparam name="T">A Blazor component derived from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameterName">Name of the parameter to pass.</param>
    /// <param name="parameterValue">Value of the parameter to pass.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Task<Result<Unit, ModalError>> ShowAsync<T>(string parameterName, object parameterValue)
        where T : DropBearComponentBase
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return Task.FromResult(Result<Unit, ModalError>.Failure(
                ModalError.ShowFailed(typeof(T).Name, "Parameter name cannot be null or empty")
            ));
        }

        var parameters = new Dictionary<string, object> { { parameterName, parameterValue } };
        return ShowAsync<T>(parameters);
    }

    /// <summary>
    ///     Closes the current modal and, if any modals are queued, displays the next one.
    /// </summary>
    public async Task<Result<Unit, ModalError>> CloseAsync()
    {
        try
        {
            // Use non-blocking wait with timeout for better thread safety
            if (!await _modalSemaphore.WaitAsync(2000))
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to close modal.");
                return Result<Unit, ModalError>.Failure(
                    ModalError.CloseFailed("Timed out waiting for modal lock")
                );
            }

            try
            {
                _logger.LogDebug("Closing the current modal.");
                CurrentComponent = null;
                CurrentParameters = null;
                _isModalVisible = false;

                // Check if we have a queued modal
                if (_modalQueue.TryDequeue(out var nextModal))
                {
                    _logger.LogDebug("Displaying next modal from queue: type {ComponentType}.",
                        nextModal.ComponentType.Name);
                    CurrentComponent = nextModal.ComponentType;
                    CurrentParameters = nextModal.Parameters;
                    _isModalVisible = true;
                }
                else
                {
                    _logger.LogDebug("No modals left in the queue.");
                }

                // Trigger UI update with debounce protection
                await NotifyStateChangedAsync();

                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing modal.");
            return Result<Unit, ModalError>.Failure(
                ModalError.CloseFailed($"Error closing modal: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Clears all modals from the queue and closes any currently displayed modal.
    /// </summary>
    public async Task<Result<Unit, ModalError>> ClearAllAsync()
    {
        try
        {
            // Use non-blocking wait with timeout for better thread safety
            if (!await _modalSemaphore.WaitAsync(2000))
            {
                _logger.LogWarning("Timed out waiting for modal semaphore to clear modals.");
                return Result<Unit, ModalError>.Failure(
                    ModalError.QueueFailed("Timed out waiting for modal lock")
                );
            }

            try
            {
                _logger.LogDebug("Clearing all modals.");

                // Clear the queue first
                while (_modalQueue.TryDequeue(out _)) { }

                // Close current modal if any
                if (_isModalVisible)
                {
                    CurrentComponent = null;
                    CurrentParameters = null;
                    _isModalVisible = false;

                    // Trigger UI update
                    await NotifyStateChangedAsync();
                }

                return Result<Unit, ModalError>.Success(Unit.Value);
            }
            finally
            {
                _modalSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all modals.");
            return Result<Unit, ModalError>.Failure(
                ModalError.QueueFailed($"Error clearing modals: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Invokes the <see cref="OnChange" /> event to notify any subscribers of the modal state update,
    ///     with debounce protection to prevent multiple rapid notifications.
    /// </summary>
    private async Task NotifyStateChangedAsync()
    {
        // Skip if already processing or disposed
        if (_isProcessingEvents)
        {
            // Increment pending count to signal more changes happened
            Interlocked.Increment(ref _pendingNotifications);
            return;
        }

        try
        {
            // First quick check without waiting
            if (!await _notificationSemaphore.WaitAsync(0))
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
                    _logger.LogDebug("Modal state changed notification triggered.");

                    // If more notifications arrived during processing, process them too
                } while (Interlocked.CompareExchange(ref _pendingNotifications, 0, 0) > 0);
            }
            finally
            {
                _isProcessingEvents = false;
                _notificationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while notifying modal state change.");
        }
    }

    /// <summary>
    ///     Disposes the service resources
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Clean up the modal queue
            while (_modalQueue.TryDequeue(out _)) { }

            // Clean up parameters
            CurrentComponent = null;
            CurrentParameters = null;
            _isModalVisible = false;

            // Dispose semaphores
            _modalSemaphore.Dispose();
            _notificationSemaphore.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing modal service");
        }
    }
}
