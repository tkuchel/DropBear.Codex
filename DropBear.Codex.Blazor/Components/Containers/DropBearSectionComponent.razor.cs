#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Serilog;
using Exception = System.Exception;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A component that conditionally renders its content based on synchronous or asynchronous predicates.
///     Implements enhanced error handling, performance optimizations, and lifecycle management.
///     Optimized for Blazor Server efficiency and memory usage.
/// </summary>
public sealed partial class DropBearSectionComponent : DropBearComponentBase
{
    // Logger for this component
    private new static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    private const string PREDICATE_CONFLICT_MESSAGE = "Cannot specify both AsyncPredicate and Predicate.";
    private const string MISSING_CONTENT_MESSAGE = "ChildContent must be provided and cannot be null.";
    private static readonly TimeSpan MinEvaluationInterval = TimeSpan.FromMilliseconds(100);
    private readonly CancellationTokenSource _renderThrottleCts = new();

    // UI rendering throttling
    private readonly SemaphoreSlim _renderThrottleSemaphore = new(1, 1);

    // State tracking for initialization and rendering
    private volatile bool _isInitialized;
    private bool _isPredicateValid;
    private Func<Task<bool>>? _lastAsyncPredicate;
    private DateTime _lastEvaluationTime = DateTime.MinValue;
    private Func<bool>? _lastSyncPredicate;

    // Change detection
    private bool _parametersChanged;
    private bool _renderQueued;
    private volatile bool _shouldRenderContent;
    private bool _wasAsyncPredicate;

    /// <summary>
    ///     Determines if the content should be rendered.
    /// </summary>
    private bool ShouldRenderContent => _isInitialized && _shouldRenderContent && !IsDisposed;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            ValidateParameters();
            CachePredicates();
            await EvaluatePredicatesAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Initialization failed");
            _shouldRenderContent = false;
        }

        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            var wasValid = _isPredicateValid;
            ValidateParameters();

            // Check if predicates have changed
            _parametersChanged = _lastSyncPredicate != Predicate || _lastAsyncPredicate != AsyncPredicate;

            // Cache the predicates for future comparison
            if (_parametersChanged)
            {
                CachePredicates();
            }

            if (ShouldRevaluatePredicates(wasValid))
            {
                // If predicates changed, queue evaluation with throttling
                await ThrottledEvaluationAsync();
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Parameter update failed");
            _shouldRenderContent = false;
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>
    ///     Cache predicates for change detection
    /// </summary>
    private void CachePredicates()
    {
        _lastSyncPredicate = Predicate;
        _lastAsyncPredicate = AsyncPredicate;
    }

    /// <summary>
    ///     Throttles predicate evaluation to prevent excessive evaluation
    /// </summary>
    private async Task ThrottledEvaluationAsync()
    {
        try
        {
            // Check if we need to throttle based on time
            var now = DateTime.UtcNow;
            if (now - _lastEvaluationTime < MinEvaluationInterval)
            {
                // If already queued, just exit
                if (_renderQueued)
                {
                    return;
                }

                // Try to acquire the semaphore without blocking
                if (!await _renderThrottleSemaphore.WaitAsync(0, _renderThrottleCts.Token))
                {
                    _renderQueued = true;
                    return;
                }

                try
                {
                    // Wait for throttle interval
                    var waitTime = MinEvaluationInterval - (now - _lastEvaluationTime);
                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime, _renderThrottleCts.Token);
                    }

                    // Now evaluate
                    await EvaluatePredicatesAsync();
                    _lastEvaluationTime = DateTime.UtcNow;
                    _renderQueued = false;
                }
                finally
                {
                    _renderThrottleSemaphore.Release();
                }
            }
            else
            {
                // Immediate evaluation
                await EvaluatePredicatesAsync();
                _lastEvaluationTime = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected during disposal
            _renderQueued = false;
        }
        catch (Exception ex)
        {
            LogThrottledEvaluationError(Logger, ex);
            _renderQueued = false;
        }
    }

    /// <summary>
    ///     Determines if predicates should be reevaluated based on parameter changes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldRevaluatePredicates(bool wasValid)
    {
        return (!wasValid && _isPredicateValid) ||
               _parametersChanged ||
               (Predicate is not null && AsyncPredicate is null);
    }

    /// <summary>
    ///     Evaluates predicates using ValueTask for better performance.
    /// </summary>
    private async ValueTask<bool> EvaluatePredicateAsync()
    {
        if (AsyncPredicate is not null)
        {
            return await AsyncPredicate();
        }

        return Predicate?.Invoke() ?? true;
    }

    /// <summary>
    ///     Evaluates predicates and updates component state.
    /// </summary>
    private async Task EvaluatePredicatesAsync([CallerMemberName] string caller = "")
    {
        try
        {
            var previousState = _shouldRenderContent;
            _shouldRenderContent = await EvaluatePredicateAsync();

            if (previousState != _shouldRenderContent)
            {
                LogRenderStateChanged(Logger, previousState, _shouldRenderContent, caller);

                await NotifyStateChangeAsync(_shouldRenderContent);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, $"Predicate evaluation failed in {caller}");
            _shouldRenderContent = false;
        }
    }

    /// <summary>
    ///     Validates component parameters.
    /// </summary>
    private void ValidateParameters()
    {
        try
        {
            if (ChildContent is null)
            {
                throw new ArgumentNullException(nameof(ChildContent), MISSING_CONTENT_MESSAGE);
            }

            if (AsyncPredicate is not null && Predicate is not null)
            {
                throw new InvalidOperationException(PREDICATE_CONFLICT_MESSAGE);
            }

            var isCurrentlyAsyncPredicate = AsyncPredicate is not null;
            if (_isPredicateValid && _wasAsyncPredicate != isCurrentlyAsyncPredicate)
            {
                _isPredicateValid = false;
                LogPredicateTypeChanged(Logger);
            }

            _wasAsyncPredicate = isCurrentlyAsyncPredicate;
            _isPredicateValid = true;
        }
        catch
        {
            _isPredicateValid = false;
            throw;
        }
    }

    /// <summary>
    ///     Handles exceptions uniformly and returns a Result object.
    /// </summary>
    private async Task<Result<Unit, ComponentError>> HandleExceptionAsync(Exception ex, string context)
    {
        LogExceptionContext(Logger, context, ex.Message, ex);

        if (!OnError.HasDelegate)
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError($"{context}: {ex.Message}")
            );
        }

        try
        {
            await OnError.InvokeAsync(ex);
            return Result<Unit, ComponentError>.Success(Unit.Value);
        }
        catch (Exception callbackEx)
        {
            LogErrorCallbackFailed(Logger, callbackEx);
            return Result<Unit, ComponentError>.Failure(
                new ComponentError($"Error callback failed: {callbackEx.Message}")
            );
        }
    }

    /// <summary>
    ///     Notifies listeners of state changes efficiently.
    /// </summary>
    private async Task NotifyStateChangeAsync(bool newState)
    {
        if (!OnRenderStateChanged.HasDelegate)
        {
            return;
        }

        try
        {
            await QueueStateHasChangedAsync((Func<Task>)(async () =>
                await OnRenderStateChanged.InvokeAsync(newState)));
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "State change notification failed");
        }
    }

    /// <summary>
    ///     Handles component disposal, ensuring resources are properly cleaned up
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await _renderThrottleCts.CancelAsync();
            _renderThrottleCts.Dispose();
            _renderThrottleSemaphore.Dispose();

            // Clear event handlers
            _lastSyncPredicate = null;
            _lastAsyncPredicate = null;
        }
        catch (Exception ex)
        {
            LogSectionComponentDisposeError(Logger, ex);
        }

        await base.DisposeAsync();
    }

    #region Parameters

    /// <summary>
    /// Determines whether the rendered content is disabled (non-interactable).
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; } = false;

    /// <summary>
    ///     A synchronous predicate function determining if the section should be rendered.
    /// </summary>
    [Parameter]
    public Func<bool>? Predicate { get; set; }

    /// <summary>
    ///     An asynchronous predicate function determining if the section should be rendered.
    /// </summary>
    [Parameter]
    public Func<Task<bool>>? AsyncPredicate { get; set; }

    /// <summary>
    ///     The content to be displayed if the predicates succeed.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Event callback triggered when the render state changes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnRenderStateChanged { get; set; }

    /// <summary>
    ///     Event callback triggered when an error occurs during predicate evaluation.
    /// </summary>
    [Parameter]
    public EventCallback<Exception> OnError { get; set; }

    #endregion

    #region Helper Methods (Logger)

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Core.Logging.LoggerFactory.Logger.ForContext<DropBearSectionComponent>());
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(DropBearSectionComponent));
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error during throttled evaluation")]
    static partial void LogThrottledEvaluationError(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Render state changed from {PreviousState} to {CurrentState} in {Caller}")]
    static partial void LogRenderStateChanged(Microsoft.Extensions.Logging.ILogger logger, bool previousState, bool currentState, string caller);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Predicate type changed, forcing re-evaluation")]
    static partial void LogPredicateTypeChanged(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{Context}: {Message}")]
    static partial void LogExceptionContext(Microsoft.Extensions.Logging.ILogger logger, string context, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error callback failed")]
    static partial void LogErrorCallbackFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error disposing section component")]
    static partial void LogSectionComponentDisposeError(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    #endregion
}
