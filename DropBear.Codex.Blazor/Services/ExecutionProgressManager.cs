#region

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

#endregion

/// <summary>
///     Represents the progress state of a single step
/// </summary>
public sealed record StepProgress(double Progress, StepStatus Status);

public sealed class ExecutionProgressManager : IExecutionProgressManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, StepProgress> _stepStates = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private DisposableBagBuilder? _bagBuilder;
    private string _currentMessage = string.Empty;
    private double _currentProgress;
    private IDisposable? _disposableBag;
    private bool _isDisposed;
    private bool _isIndeterminateMode;
    private bool _isSteppedMode;
    private DropBearProgressBar? _progressBar;

    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    public event Action<ProgressManagerState>? OnStateChanged;

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

    public Result<Unit, ProgressManagerError> SetIndeterminateMode(string message)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = true;
            _isSteppedMode = false;
            _currentMessage = message;

            NotifyStateChanged();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set indeterminate mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set indeterminate mode", ex));
        }
    }

    public Result<Unit, ProgressManagerError> SetNormalMode()
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = false;
            _currentProgress = 0;

            NotifyStateChanged();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set normal mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set normal mode", ex));
        }
    }

    public Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = true;

            // Initialize step states
            _stepStates.Clear();
            foreach (var step in steps)
            {
                _stepStates[step.Id] = new StepProgress(0, StepStatus.NotStarted);
            }

            NotifyStateChanged();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set stepped mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set stepped mode", ex));
        }
    }

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
                // Update internal state
                _stepStates[stepId] = new StepProgress(progress, status);

                // Update progress bar step state
                await _progressBar!.UpdateStepProgressAsync(stepId, progress, status);

                NotifyStateChanged();

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

                NotifyStateChanged();

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
                    foreach (var (stepId, stepProgress) in _stepStates)
                    {
                        if (stepProgress.Status != StepStatus.Completed &&
                            stepProgress.Status != StepStatus.Failed)
                        {
                            await _progressBar!.UpdateStepProgressAsync(stepId, 100, StepStatus.Completed);
                            _stepStates[stepId] = new StepProgress(100, StepStatus.Completed);
                        }
                    }
                }
                else
                {
                    await UpdateProgressAsync(100, "Completed");
                }

                NotifyStateChanged();
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
            DisableExecutionEngineIntegration();

            _bagBuilder = DisposableBag.CreateBuilder();

            taskProgressSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskProgressAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskStartedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskStartedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskCompletedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskCompletedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

            taskFailedSubscriber.Subscribe(channelId, message =>
                _ = HandleTaskFailedAsync(message, CancellationToken.None)).AddTo(_bagBuilder);

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

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(new ProgressManagerState(
            _isIndeterminateMode,
            _currentMessage,
            _currentProgress,
            _progressBar?.Steps,
            _stepStates));
    }

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

    private void DisableExecutionEngineIntegration()
    {
        _disposableBag?.Dispose();
        _disposableBag = null;
        _bagBuilder = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProgressManager));
        }
    }

    private void EnsureProgressBarInitialized()
    {
        if (_progressBar == null)
        {
            throw new InvalidOperationException("Progress bar not initialized. Call Initialize first.");
        }
    }
}
