#region

using System.Diagnostics;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;
using Serilog;

#endregion

namespace DropBear.Codex.Workflow.Core;

/// <summary>
///     Main workflow execution engine that processes workflow definitions and manages step execution.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public WorkflowEngine(IServiceProvider serviceProvider, ILogger logger)
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

        _logger.Debug("Disposing WorkflowEngine");
        await Task.CompletedTask.ConfigureAwait(false);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class =>
        ExecuteAsync(definition, context, WorkflowExecutionOptions.Default, cancellationToken);

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        string correlationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        var executionTrace = new List<StepExecutionTrace>();

        using Activity? activity = CreateActivity(definition.WorkflowId, correlationId);

        _logger.Information(
            "Starting workflow {WorkflowId} (Correlation: {CorrelationId})",
            definition.WorkflowId,
            correlationId);

        try
        {
            IWorkflowNode<TContext> rootNode = definition.BuildWorkflow();
            NodeExecutionResult nodeResult = await ExecuteNodeAsync(
                rootNode,
                context,
                options,
                executionTrace,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            if (!nodeResult.IsSuccess || nodeResult.IsSuspension)
            {
                WorkflowMetrics metrics = BuildMetrics(executionTrace, stopwatch.Elapsed);

                if (nodeResult is { IsSuspension: true, SuspensionInfo: not null })
                {
                    SuspensionInfo suspensionInfo = nodeResult.SuspensionInfo.Value;
                    _logger.Information(
                        "Workflow {WorkflowId} suspended, waiting for signal: {SignalName} (Correlation: {CorrelationId})",
                        definition.WorkflowId,
                        suspensionInfo.SignalName,
                        correlationId);

                    return WorkflowResult<TContext>.Suspended(
                        context,
                        suspensionInfo.SignalName,
                        suspensionInfo.Metadata,
                        metrics,
                        executionTrace,
                        correlationId);
                }

                // Run compensation if workflow failed
                if (!nodeResult.IsSuccess)
                {
                    await RunCompensationAsync(executionTrace, context, cancellationToken).ConfigureAwait(false);
                }

                _logger.Warning(
                    "Workflow {WorkflowId} failed: {ErrorMessage} (Correlation: {CorrelationId})",
                    definition.WorkflowId,
                    nodeResult.ErrorMessage,
                    correlationId);

                activity?.SetTag("workflow.status", "failed");

                return nodeResult.Exception != null
                    ? WorkflowResult<TContext>.Failure(context, nodeResult.Exception, metrics, executionTrace,
                        correlationId)
                    : WorkflowResult<TContext>.Failure(context, nodeResult.ErrorMessage ?? "Workflow execution failed",
                        metrics, executionTrace, correlationId);
            }

            _logger.Information(
                "Workflow {WorkflowId} completed successfully in {Duration}ms (Correlation: {CorrelationId})",
                definition.WorkflowId,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            activity?.SetTag("workflow.status", "completed");
            activity?.SetTag("workflow.duration_ms", stopwatch.ElapsedMilliseconds);

            WorkflowMetrics finalMetrics = BuildMetrics(executionTrace, stopwatch.Elapsed);
            return WorkflowResult<TContext>.Success(context, finalMetrics, executionTrace, correlationId);
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            _logger.Warning(
                ex,
                "Workflow {WorkflowId} was cancelled (Correlation: {CorrelationId})",
                definition.WorkflowId,
                correlationId);

            activity?.SetTag("workflow.status", "cancelled");

            WorkflowMetrics metrics = BuildMetrics(executionTrace, stopwatch.Elapsed);
            return WorkflowResult<TContext>.Failure(context, ex, metrics, executionTrace, correlationId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(
                ex,
                "Workflow {WorkflowId} encountered an unexpected error (Correlation: {CorrelationId})",
                definition.WorkflowId,
                correlationId);

            activity?.SetTag("workflow.status", "error");
            activity?.SetTag("workflow.error", ex.Message);

            WorkflowMetrics metrics = BuildMetrics(executionTrace, stopwatch.Elapsed);
            return WorkflowResult<TContext>.Failure(context, ex, metrics, executionTrace, correlationId);
        }
    }

    private async ValueTask<NodeExecutionResult> ExecuteNodeAsync<TContext>(
        IWorkflowNode<TContext> node,
        TContext context,
        WorkflowExecutionOptions options,
        List<StepExecutionTrace> executionTrace,
        CancellationToken cancellationToken) where TContext : class
    {
        NodeExecutionResult<TContext>
            nodeResult = await node.ExecuteAsync(context, _serviceProvider, cancellationToken).ConfigureAwait(false);

        // Add step execution to trace
        if (nodeResult.StepTrace is not null)
        {
            executionTrace.Add(nodeResult.StepTrace);

            if (executionTrace.Count > WorkflowConstants.Limits.MaxExecutionTraceEntries)
            {
                _logger.Warning(
                    "Execution trace exceeded maximum entries ({MaxEntries}), removing oldest entries",
                    WorkflowConstants.Limits.MaxExecutionTraceEntries);

                executionTrace.RemoveRange(0, executionTrace.Count - WorkflowConstants.Limits.MaxExecutionTraceEntries);
            }
        }

        // Check for suspension
        if (!nodeResult.StepResult.IsSuccess &&
            WorkflowConstants.Signals.IsSuspensionSignal(nodeResult.StepResult.Error?.Message))
        {
            string? signalName = WorkflowConstants.Signals.ExtractSignalName(nodeResult.StepResult.Error?.Message);
            return new NodeExecutionResult
            {
                IsSuccess = false,
                IsSuspension = true,
                SuspensionInfo = new SuspensionInfo
                {
                    SignalName = signalName!, Metadata = nodeResult.StepResult.Metadata
                }
            };
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
            foreach (IWorkflowNode<TContext> nextNode in nodeResult.NextNodes)
            {
                NodeExecutionResult result =
                    await ExecuteNodeAsync(nextNode, context, options, executionTrace, cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess || result.IsSuspension)
                {
                    return result;
                }
            }
        }

        return new NodeExecutionResult { IsSuccess = true };
    }

    private async ValueTask RunCompensationAsync<TContext>(
        List<StepExecutionTrace> executionTrace,
        TContext context,
        CancellationToken cancellationToken) where TContext : class
    {
        _logger.Information("Running compensation for {Count} executed steps", executionTrace.Count);

        // Execute compensation in reverse order
        for (int i = executionTrace.Count - 1; i >= 0; i--)
        {
            StepExecutionTrace trace = executionTrace[i];
            if (!trace.Result.IsSuccess)
            {
                continue;
            }

            try
            {
                _logger.Debug("Compensating step {StepName}", trace.StepName);
                // Compensation logic would be executed here via step.CompensateAsync
                // This requires access to the original step instance, which we'd need to track
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Compensation failed for step {StepName}", trace.StepName);
            }
        }
    }

    private static WorkflowMetrics BuildMetrics(List<StepExecutionTrace> executionTrace, TimeSpan totalDuration)
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

    private static Activity? CreateActivity(string workflowId, string correlationId)
    {
        var activity = new Activity($"{WorkflowConstants.Monitoring.ActivityNamePrefix}.WorkflowExecution");
        activity.SetTag("workflow.id", workflowId);
        activity.SetTag("correlation.id", correlationId);
        return activity.Start();
    }

    private record struct NodeExecutionResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }
        public bool IsSuspension { get; init; }
        public SuspensionInfo? SuspensionInfo { get; init; }
    }

    private record struct SuspensionInfo
    {
        public string SignalName { get; init; }
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    }
}
