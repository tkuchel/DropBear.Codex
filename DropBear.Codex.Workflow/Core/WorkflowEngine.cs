#region

using System.Diagnostics;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Core;

/// <summary>
///     Core workflow execution engine with support for metrics, tracing, and error handling.
/// </summary>
public sealed partial class WorkflowEngine : IWorkflowEngine, IAsyncDisposable
{
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the workflow engine.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider or logger is null</exception>
    public WorkflowEngine(IServiceProvider serviceProvider, ILogger<WorkflowEngine> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        LogWorkflowEngineDisposing();
        _disposed = true;

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var defaultOptions = new WorkflowExecutionOptions();
        return await ExecuteInternalAsync(definition, context, defaultOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await ExecuteInternalAsync(definition, context, options, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowResult<TContext>> ExecuteInternalAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        string correlationId = options.CorrelationId ?? Guid.NewGuid().ToString("N");
        using Activity? activity = options.EnableTracing
            ? CreateActivity(definition.WorkflowId, correlationId)
            : null;

        LogWorkflowStarting(definition.WorkflowId, definition.DisplayName, correlationId);

        var stopwatch = Stopwatch.StartNew();
        var executionTrace = new CircularExecutionTrace<StepExecutionTrace>(
            WorkflowConstants.Limits.MaxExecutionTraceEntries);

        try
        {
            IWorkflowNode<TContext> rootNode = definition.BuildWorkflow();
            TimeSpan? effectiveTimeout = options.WorkflowTimeout ?? definition.WorkflowTimeout;

            NodeExecutionResult result = effectiveTimeout.HasValue
                ? await ExecuteWithTimeoutAsync(rootNode, context, options, executionTrace, effectiveTimeout.Value,
                    cancellationToken).ConfigureAwait(false)
                : await ExecuteNodeInternalAsync(rootNode, context, options, executionTrace, cancellationToken)
                    .ConfigureAwait(false);

            stopwatch.Stop();

            return result switch
            {
                { IsSuspension: true, SuspendedInfo: not null } =>
                    HandleSuspension(result, context, executionTrace, stopwatch.Elapsed, correlationId, activity,
                        definition.WorkflowId),
                { IsSuccess: false } =>
                    await HandleFailureAsync(result, context, executionTrace, stopwatch.Elapsed, correlationId,
                        activity, options, definition.WorkflowId, cancellationToken).ConfigureAwait(false),
                _ =>
                    HandleSuccess(context, executionTrace, stopwatch.Elapsed, correlationId, activity,
                        definition.WorkflowId)
            };
        }
        catch (OperationCanceledException)
        {
            return HandleCancellation(context, executionTrace, stopwatch, correlationId, activity,
                definition.WorkflowId);
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(context, executionTrace, stopwatch, correlationId, activity,
                definition.WorkflowId, ex);
        }
        catch (TimeoutException ex)
        {
            return HandleTimeout(context, executionTrace, stopwatch, correlationId, activity, definition.WorkflowId,
                ex);
        }
    }

    private WorkflowResult<TContext> HandleSuspension<TContext>(
        NodeExecutionResult result,
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        TimeSpan duration,
        string correlationId,
        Activity? activity,
        string workflowId) where TContext : class
    {
        string signalName = result.SuspendedInfo!.SignalName;

        LogWorkflowSuspended(workflowId, signalName, correlationId);

        activity?.SetTag("workflow.status", "suspended");
        activity?.SetTag("workflow.signal", signalName);

        WorkflowMetrics metrics = BuildMetrics(executionTrace.ToList(), duration);
        return WorkflowResult<TContext>.Suspended(
            context,
            signalName,
            result.SuspendedInfo.Metadata,
            metrics,
            executionTrace.ToList(),
            correlationId);
    }

    private async ValueTask<WorkflowResult<TContext>> HandleFailureAsync<TContext>(
        NodeExecutionResult result,
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        TimeSpan duration,
        string correlationId,
        Activity? activity,
        WorkflowExecutionOptions options,
        string workflowId,
        CancellationToken cancellationToken) where TContext : class
    {
        LogWorkflowFailed(workflowId, result.ErrorMessage ?? "Unknown error");

        List<CompensationFailure>? compensationFailures = null;
        if (options.EnableCompensation && executionTrace.Count > 0)
        {
            compensationFailures = await RunCompensationAsync(context, executionTrace.ToList(), cancellationToken)
                .ConfigureAwait(false);

            // Add compensation failure info to activity
            if (compensationFailures.Count > 0)
            {
                activity?.SetTag("workflow.compensation_failures", compensationFailures.Count);
            }
        }

        activity?.SetTag("workflow.status", "failed");
        activity?.SetTag("workflow.error", result.ErrorMessage);

        WorkflowMetrics failureMetrics = BuildMetrics(executionTrace.ToList(), duration);
        return result.Exception != null
            ? WorkflowResult<TContext>.Failure(context, result.Exception, failureMetrics, executionTrace.ToList(),
                correlationId, compensationFailures)
            : WorkflowResult<TContext>.Failure(context, result.ErrorMessage ?? "Unknown error",
                failureMetrics, executionTrace.ToList(), correlationId, compensationFailures);
    }

    private WorkflowResult<TContext> HandleSuccess<TContext>(
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        TimeSpan duration,
        string correlationId,
        Activity? activity,
        string workflowId) where TContext : class
    {
        LogWorkflowCompleted(workflowId, duration.TotalMilliseconds);

        activity?.SetTag("workflow.status", "completed");

        WorkflowMetrics successMetrics = BuildMetrics(executionTrace.ToList(), duration);
        return WorkflowResult<TContext>.Success(context, successMetrics, executionTrace.ToList(), correlationId);
    }

    private WorkflowResult<TContext> HandleCancellation<TContext>(
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        Stopwatch stopwatch,
        string correlationId,
        Activity? activity,
        string workflowId) where TContext : class
    {
        LogWorkflowCancelled(workflowId);
        stopwatch.Stop();

        activity?.SetTag("workflow.status", "cancelled");

        WorkflowMetrics cancelMetrics = BuildMetrics(executionTrace.ToList(), stopwatch.Elapsed);
        return WorkflowResult<TContext>.Failure(context, "Workflow execution was cancelled", cancelMetrics,
            executionTrace.ToList(), correlationId);
    }

    private WorkflowResult<TContext> HandleInvalidOperation<TContext>(
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        Stopwatch stopwatch,
        string correlationId,
        Activity? activity,
        string workflowId,
        InvalidOperationException ex) where TContext : class
    {
        LogWorkflowInvalidOperation(workflowId, ex);
        stopwatch.Stop();

        activity?.SetTag("workflow.status", "error");
        activity?.SetTag("workflow.error", ex.Message);

        WorkflowMetrics errorMetrics = BuildMetrics(executionTrace.ToList(), stopwatch.Elapsed);
        return WorkflowResult<TContext>.Failure(context, ex, errorMetrics, executionTrace.ToList(), correlationId);
    }

    private WorkflowResult<TContext> HandleTimeout<TContext>(
        TContext context,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        Stopwatch stopwatch,
        string correlationId,
        Activity? activity,
        string workflowId,
        TimeoutException ex) where TContext : class
    {
        LogWorkflowTimeout(workflowId, ex);
        stopwatch.Stop();

        activity?.SetTag("workflow.status", "timeout");
        activity?.SetTag("workflow.error", ex.Message);

        WorkflowMetrics errorMetrics = BuildMetrics(executionTrace.ToList(), stopwatch.Elapsed);
        return WorkflowResult<TContext>.Failure(context, ex, errorMetrics, executionTrace.ToList(), correlationId);
    }

    private async ValueTask<NodeExecutionResult> ExecuteWithTimeoutAsync<TContext>(
        IWorkflowNode<TContext> node,
        TContext context,
        WorkflowExecutionOptions options,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        TimeSpan timeout,
        CancellationToken cancellationToken) where TContext : class
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await ExecuteNodeInternalAsync(node, context, options, executionTrace, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogWorkflowTimeoutExceeded(timeout);
            return new NodeExecutionResult
            {
                IsSuccess = false, ErrorMessage = $"Workflow exceeded timeout of {timeout.TotalSeconds} seconds"
            };
        }
    }

    private async ValueTask<NodeExecutionResult> ExecuteNodeInternalAsync<TContext>(
        IWorkflowNode<TContext> node,
        TContext context,
        WorkflowExecutionOptions options,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        CancellationToken cancellationToken) where TContext : class
    {
        NodeExecutionResult<TContext> nodeResult =
            await node.ExecuteAsync(context, _serviceProvider, cancellationToken).ConfigureAwait(false);

        if (nodeResult.StepTrace is not null)
        {
            AddStepTrace(executionTrace, nodeResult.StepTrace);
        }

        // Check for suspension
        if (!nodeResult.StepResult.IsSuccess &&
            WorkflowConstants.Signals.IsSuspensionSignal(nodeResult.StepResult.Error?.Message))
        {
            return CreateSuspensionResult(nodeResult);
        }

        // Handle failure
        if (!nodeResult.StepResult.IsSuccess)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                ErrorMessage = nodeResult.StepResult.Error?.Message,
                Exception = nodeResult.StepResult.Error?.SourceException
            };
        }

        // Process next nodes recursively
        if (nodeResult.NextNodes.Count > 0)
        {
            return await ExecuteNextNodesAsync(nodeResult.NextNodes, context, options, executionTrace,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new NodeExecutionResult { IsSuccess = true };
    }

    private void AddStepTrace(CircularExecutionTrace<StepExecutionTrace> executionTrace, StepExecutionTrace stepTrace)
    {
        // Log warning if we're about to overwrite oldest entries
        if (executionTrace.IsFull)
        {
            LogExecutionTraceExceeded(WorkflowConstants.Limits.MaxExecutionTraceEntries);
        }

        executionTrace.Add(stepTrace);
    }

    private static NodeExecutionResult CreateSuspensionResult<TContext>(NodeExecutionResult<TContext> nodeResult)
        where TContext : class
    {
        string? signalName = WorkflowConstants.Signals.ExtractSignalName(nodeResult.StepResult.Error?.Message);
        return new NodeExecutionResult
        {
            IsSuccess = false,
            IsSuspension = true,
            SuspendedInfo = new WorkflowSuspensionInfo
            {
                SignalName = signalName!, Metadata = nodeResult.StepResult.Metadata
            }
        };
    }

    private async ValueTask<NodeExecutionResult> ExecuteNextNodesAsync<TContext>(
        IReadOnlyList<IWorkflowNode<TContext>> nextNodes,
        TContext context,
        WorkflowExecutionOptions options,
        CircularExecutionTrace<StepExecutionTrace> executionTrace,
        CancellationToken cancellationToken) where TContext : class
    {
        foreach (IWorkflowNode<TContext> nextNode in nextNodes)
        {
            NodeExecutionResult result =
                await ExecuteNodeInternalAsync(nextNode, context, options, executionTrace, cancellationToken)
                    .ConfigureAwait(false);

            if (!result.IsSuccess || result.IsSuspension)
            {
                return result;
            }
        }

        return new NodeExecutionResult { IsSuccess = true };
    }

    /// <summary>
    ///     Executes compensation logic for all successfully executed steps in reverse order.
    ///     Implements the Saga pattern for workflow rollback.
    /// </summary>
    /// <returns>A list of compensation failures, empty if all compensations succeeded</returns>
    private async ValueTask<List<CompensationFailure>> RunCompensationAsync<TContext>(
        TContext context,
        IReadOnlyList<StepExecutionTrace> executionTrace,
        CancellationToken cancellationToken) where TContext : class
    {
        LogCompensationStarting(executionTrace.Count);

        int compensatedCount = 0;
        var failures = new List<CompensationFailure>();

        // Execute compensation in reverse order (LIFO - Last In First Out)
        for (int i = executionTrace.Count - 1; i >= 0; i--)
        {
            var result = await TryCompensateStepAsync(context, executionTrace[i], cancellationToken)
                .ConfigureAwait(false);

            if (result is not null)
            {
                failures.Add(result);
            }
            else if (executionTrace[i].Result.IsSuccess)
            {
                compensatedCount++;
            }
        }

        LogCompensationSummary(compensatedCount, failures.Count);
        return failures;
    }

    /// <summary>
    ///     Attempts to compensate a single step.
    /// </summary>
    /// <returns>
    ///     A CompensationFailure if compensation failed, or null if compensation succeeded or was skipped.
    /// </returns>
    private async ValueTask<CompensationFailure?> TryCompensateStepAsync<TContext>(
        TContext context,
        StepExecutionTrace trace,
        CancellationToken cancellationToken) where TContext : class
    {
        // Only compensate successfully executed steps
        if (!trace.Result.IsSuccess)
        {
            return null; // Not an error, just skipped
        }

        // Skip steps without type information
        if (!ValidateStepTrace(trace, typeof(TContext)))
        {
            return null; // Not an error, just skipped
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogCompensatingStep(trace.StepName);

            bool success = await ExecuteStepCompensationAsync(context, trace, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                return new CompensationFailure(
                    trace.StepName,
                    "Compensation returned failure status",
                    null);
            }

            return null; // Success
        }
        catch (OperationCanceledException)
        {
            LogCompensationCancelled(trace.StepName);
            throw;
        }
#pragma warning disable CA1031 // Catching all exceptions during compensation is intentional
        catch (Exception ex)
        {
            LogCompensationFailed(trace.StepName, ex);
            return new CompensationFailure(
                trace.StepName,
                $"Exception during compensation: {ex.Message}",
                ex);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    ///     Validates that a step trace has the required information for compensation.
    /// </summary>
    private static bool ValidateStepTrace(StepExecutionTrace trace, Type expectedContextType)
    {
        return trace.StepType != null &&
               trace.ContextType != null &&
               trace.ContextType == expectedContextType;
    }

    /// <summary>
    ///     Executes the compensation logic for a specific step.
    /// </summary>
    private async ValueTask<bool> ExecuteStepCompensationAsync<TContext>(
        TContext context,
        StepExecutionTrace trace,
        CancellationToken cancellationToken) where TContext : class
    {
        object? stepInstance = _serviceProvider.GetService(trace.StepType!);

        if (stepInstance is not IWorkflowStep<TContext> typedStep)
        {
            LogCompensationSkipped(trace.StepName, "Unable to resolve step instance");
            return true; // Not an error, just skipped
        }

        StepResult compensationResult = await typedStep
            .CompensateAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (compensationResult.IsSuccess)
        {
            LogCompensationStepSucceeded(trace.StepName);
            return true;
        }

        string errorMessage = compensationResult.Error?.Message ?? "Unknown compensation error";
        LogCompensationStepFailed(trace.StepName, errorMessage);

        if (compensationResult.Error?.SourceException is InvalidOperationException invalidOpEx)
        {
            throw invalidOpEx;
        }

        throw new InvalidOperationException(errorMessage, compensationResult.Error?.SourceException);
    }

    private static WorkflowMetrics BuildMetrics(IReadOnlyList<StepExecutionTrace> executionTrace, TimeSpan totalDuration)
    {
        int stepsExecuted = executionTrace.Count;
        int stepsFailed = executionTrace.Count(t => !t.Result.IsSuccess);
        int stepsSucceeded = stepsExecuted - stepsFailed;
        int totalRetries = executionTrace.Sum(t => t.RetryAttempts);

        return new WorkflowMetrics
        {
            TotalExecutionTime = totalDuration,
            StepsExecuted = stepsExecuted,
            StepsSucceeded = stepsSucceeded,
            StepsFailed = stepsFailed,
            TotalRetries = totalRetries,
            AverageStepExecutionTime = stepsExecuted > 0
                ? TimeSpan.FromMilliseconds(executionTrace.Average(t => t.ExecutionTime.TotalMilliseconds))
                : TimeSpan.Zero
        };
    }

    private static Activity CreateActivity(string workflowId, string correlationId)
    {
        using var activity = new Activity($"{WorkflowConstants.Monitoring.ActivityNamePrefix}.WorkflowExecution");
        activity.SetTag("workflow.id", workflowId);
        activity.SetTag("correlation.id", correlationId);
        return activity.Start();
    }

    // Logger message source generators
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Disposing WorkflowEngine")]
    partial void LogWorkflowEngineDisposing();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting workflow execution: {WorkflowId} ({DisplayName}) with correlation ID {CorrelationId}")]
    partial void LogWorkflowStarting(string workflowId, string displayName, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message =
            "Workflow {WorkflowId} suspended - waiting for signal: {SignalName} (Correlation ID: {CorrelationId})")]
    partial void LogWorkflowSuspended(string workflowId, string signalName, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow {WorkflowId} failed: {ErrorMessage}")]
    partial void LogWorkflowFailed(string workflowId, string errorMessage);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workflow {WorkflowId} completed successfully in {Duration}ms")]
    partial void LogWorkflowCompleted(string workflowId, double duration);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow {WorkflowId} was cancelled")]
    partial void LogWorkflowCancelled(string workflowId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Workflow {WorkflowId} failed with invalid operation")]
    partial void LogWorkflowInvalidOperation(string workflowId, InvalidOperationException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Workflow {WorkflowId} failed with timeout")]
    partial void LogWorkflowTimeout(string workflowId, TimeoutException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Workflow exceeded timeout of {Timeout}")]
    partial void LogWorkflowTimeoutExceeded(TimeSpan timeout);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Execution trace exceeded maximum entries ({MaxEntries}), removing oldest entries")]
    partial void LogExecutionTraceExceeded(int maxEntries);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Running compensation for {Count} executed steps")]
    partial void LogCompensationStarting(int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Compensating step {StepName}")]
    partial void LogCompensatingStep(string stepName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Compensation cancelled for step {StepName}")]
    partial void LogCompensationCancelled(string stepName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Compensation failed for step {StepName}")]
    partial void LogCompensationFailed(string stepName, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Skipping compensation for step {StepName}: {Reason}")]
    partial void LogCompensationSkipped(string stepName, string reason);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Compensation succeeded for step {StepName}")]
    partial void LogCompensationStepSucceeded(string stepName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Compensation failed for step {StepName}: {ErrorMessage}")]
    partial void LogCompensationStepFailed(string stepName, string errorMessage);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Compensation completed - {CompensatedCount} steps compensated, {ErrorCount} errors")]
    partial void LogCompensationSummary(int compensatedCount, int errorCount);

    private readonly record struct NodeExecutionResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }
        public bool IsSuspension { get; init; }
        public WorkflowSuspensionInfo? SuspendedInfo { get; init; }
    }

    private sealed record WorkflowSuspensionInfo
    {
        public required string SignalName { get; init; }
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    }
}
