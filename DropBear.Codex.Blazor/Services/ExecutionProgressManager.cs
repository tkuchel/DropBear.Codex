#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages progress updates and state transitions for progress bars in a Blazor application.
///     Supports multiple progress modes and optional integration with an execution engine.
/// </summary>
public sealed class ExecutionProgressManager : IExecutionProgressManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, StepStatus> _stepStatuses = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // MessagePipe subscription management
    private DisposableBagBuilder? _bagBuilder;
    private string _currentMessage = string.Empty;
    private double _currentProgress;
    private IDisposable? _disposableBag;
    private bool _isDisposed;
    private bool _isIndeterminateMode;
    private bool _isSteppedMode;

    private DropBearProgressBar? _progressBar;

    /// <summary>
    ///     Initializes a new instance of the ProgressManager class.
    /// </summary>
    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> Initialize(DropBearProgressBar progressBar)
    {
        ThrowIfDisposed();

        try
        {
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize progress manager");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to initialize progress manager", ex));
        }
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetIndeterminateMode(string message)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            // Update our internal state
            _isIndeterminateMode = true;
            _isSteppedMode = false;
            _currentMessage = message;

            // Let the parent component update the progress bar through parameter binding
            OnStateChanged?.Invoke(new ProgressManagerState(true, message, 0, null));

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set indeterminate mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set indeterminate mode", ex));
        }
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetNormalMode()
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            // Update our internal state
            _isIndeterminateMode = false;
            _isSteppedMode = false;
            _currentProgress = 0;

            // Let the parent component update the progress bar through parameter binding
            OnStateChanged?.Invoke(new ProgressManagerState(false, _currentMessage, 0, null));

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set normal mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set normal mode", ex));
        }
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            // Update our internal state
            _isIndeterminateMode = false;
            _isSteppedMode = true;

            // Let the parent component update the progress bar through parameter binding
            OnStateChanged?.Invoke(new ProgressManagerState(false, _currentMessage, 0, steps));

            // Initialize step statuses
            _stepStatuses.Clear();
            foreach (var step in steps)
            {
                _stepStatuses[step.Id] = StepStatus.NotStarted;
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set stepped mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set stepped mode", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateProgressAsync(
        double progress,
        string? message = null)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (_isIndeterminateMode)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update progress in indeterminate mode"));
        }

        try
        {
            await _updateLock.WaitAsync();
            try
            {
                _currentProgress = Math.Clamp(progress, 0, 100);
                if (message != null)
                {
                    _currentMessage = message;
                }

                // Notify of state change
                OnStateChanged?.Invoke(new ProgressManagerState(false, _currentMessage, _currentProgress, null));

                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update progress", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateStepProgressAsync(
        string stepId,
        double progress,
        StepStatus status)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (!_isSteppedMode)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update step progress when not in stepped mode"));
        }

        try
        {
            await _updateLock.WaitAsync();
            try
            {
                // Use the component's built-in step progress update method
                await _progressBar!.UpdateStepProgressAsync(stepId, progress, status);
                _stepStatuses[stepId] = status;

                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update step progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update step progress", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync()
    {
        ThrowIfDisposed();

        try
        {
            await _updateLock.WaitAsync();
            try
            {
                if (_isSteppedMode)
                {
                    foreach (var (stepId, status) in _stepStatuses)
                    {
                        if (status != StepStatus.Completed && status != StepStatus.Failed)
                        {
                            await _progressBar!.UpdateStepProgressAsync(stepId, 100, StepStatus.Completed);
                        }
                    }
                }
                else
                {
                    await UpdateProgressAsync(100, "Completed");
                }

                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to complete progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to complete progress", ex));
        }
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> EnableExecutionEngineIntegration(
        Guid channelId,
        ISubscriber<Guid, TaskStartedMessage> taskStartedSubscriber,
        ISubscriber<Guid, TaskProgressMessage> taskProgressSubscriber,
        ISubscriber<Guid, TaskCompletedMessage> taskCompletedSubscriber,
        ISubscriber<Guid, TaskFailedMessage> taskFailedSubscriber)
    {
        ThrowIfDisposed();

        try
        {
            // Clean up any existing subscriptions
            DisableExecutionEngineIntegration();

            // Create new bag builder
            _bagBuilder = DisposableBag.CreateBuilder();

            // Set up subscriptions using Action delegates to handle async methods
            taskProgressSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskProgressAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskStartedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskStartedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskCompletedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskCompletedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskFailedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskFailedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            // Build the disposable bag
            _disposableBag = _bagBuilder.Build();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to enable execution engine integration");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to enable execution engine integration", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        DisableExecutionEngineIntegration();
        _updateLock.Dispose();
        _progressBar = null;
    }

    /// <summary>
    ///     Event raised when the progress manager's state changes
    /// </summary>
    public event Action<ProgressManagerState>? OnStateChanged;

    private async Task HandleTaskProgressAsync(TaskProgressMessage message, CancellationToken cancellationToken)
    {
        if (_isDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            if (message.TaskProgressPercentage.HasValue)
            {
                await UpdateStepProgressAsync(
                    message.TaskName,
                    message.TaskProgressPercentage.Value,
                    StepStatus.InProgress);
            }
        }
        else if (!_isIndeterminateMode && message.OverallProgressPercentage.HasValue)
        {
            await UpdateProgressAsync(
                message.OverallProgressPercentage.Value,
                message.Message);
        }
    }

    private async Task HandleTaskStartedAsync(TaskStartedMessage message, CancellationToken cancellationToken)
    {
        if (_isDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(
                message.TaskName,
                0,
                StepStatus.InProgress);
        }
    }

    private async Task HandleTaskCompletedAsync(TaskCompletedMessage message, CancellationToken cancellationToken)
    {
        if (_isDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(
                message.TaskName,
                100,
                StepStatus.Completed);
        }
    }

    private async Task HandleTaskFailedAsync(TaskFailedMessage message, CancellationToken cancellationToken)
    {
        if (_isDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(
                message.TaskName,
                0,
                StepStatus.Failed);
        }
    }

    /// <summary>
    ///     Disables and cleans up execution engine integration.
    /// </summary>
    private void DisableExecutionEngineIntegration()
    {
        _disposableBag?.Dispose();
        _disposableBag = null;
        _bagBuilder = null;
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if the manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProgressManager));
        }
    }

    /// <summary>
    ///     Ensures the progress bar has been initialized.
    /// </summary>
    private void EnsureProgressBarInitialized()
    {
        if (_progressBar == null)
        {
            throw new InvalidOperationException("Progress bar not initialized. Call Initialize first.");
        }
    }

    /// <summary>
    ///     Represents the current state of the progress manager
    /// </summary>
    public sealed record ProgressManagerState(
        bool IsIndeterminate,
        string Message,
        double Progress,
        IReadOnlyList<ProgressStepConfig>? Steps);
}

// EXAMPLE USAGE:
// <DropBearProgressBar @ref="_progressBar"
// IsIndeterminate="@_state.IsIndeterminate"
// Message="@_state.Message"
// Progress="@_state.Progress"
// Steps="@_state.Steps" />
//
//       @code {
// private DropBearProgressBar _progressBar;
// private ProgressManagerState _state = new(false, string.Empty, 0, null);
// private readonly IProgressManager _progressManager;
//
// protected override void OnInitialized()
// {
//     _progressManager.Initialize(_progressBar);
//     _progressManager.OnStateChanged += state =>
//     {
//         _state = state;
//         InvokeAsync(StateHasChanged);
//     };
// }
// }
