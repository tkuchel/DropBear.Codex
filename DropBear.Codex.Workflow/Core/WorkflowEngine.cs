using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Metrics;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// FIXED: WorkflowEngine that properly processes NextNodes from successful step executions
/// and correctly propagates error messages from failed steps.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Serilog.ILogger _logger;

    public WorkflowEngine(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = LoggerFactory.Logger.ForContext<WorkflowEngine>() ?? throw new ArgumentNullException(nameof(_logger));
    }

    public ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return ExecuteAsync(definition, context, WorkflowExecutionOptions.Default, cancellationToken);
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        var executionTrace = new List<StepExecutionTrace>();

        using var activity = CreateActivity(definition.WorkflowId, correlationId);

        _logger.Information("Starting workflow execution: {WorkflowId} (Correlation: {CorrelationId})",
            definition.WorkflowId, correlationId);

        try
        {
            using var workflowCts = definition.WorkflowTimeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (workflowCts is not null)
            {
                workflowCts.CancelAfter(definition.WorkflowTimeout.Value);
            }

            var effectiveCancellationToken = workflowCts?.Token ?? cancellationToken;

            var rootNode = definition.BuildWorkflow();
            if (rootNode is null)
            {
                throw new InvalidOperationException($"Workflow '{definition.WorkflowId}' returned null root node");
            }

            var executionContext = new WorkflowExecutionContext<TContext>
            {
                Options = options,
                CorrelationId = correlationId,
                ExecutionTrace = executionTrace,
                ServiceProvider = _serviceProvider,
                Logger = _logger
            };

            // CRITICAL FIX: Use the corrected workflow graph execution with proper error handling
            var executionResult = await ExecuteWorkflowGraphFixed<TContext>(
                rootNode,
                context,
                executionContext,
                effectiveCancellationToken);

            stopwatch.Stop();

            var finalMetrics = new WorkflowExecutionMetrics
            {
                TotalExecutionTime = stopwatch.Elapsed,
                StepsExecuted = executionTrace.Count(t => t.Result.IsSuccess),
                StepsFailed = executionTrace.Count(t => !t.Result.IsSuccess),
                RetryAttempts = executionTrace.Sum(t => t.RetryAttempts),
                PeakMemoryUsage = options.EnableMemoryMetrics ? GC.GetTotalMemory(false) : null,
                CustomMetrics = options.CustomOptions
            };

            // FIXED: Handle suspension signals properly
            if (executionResult.suspensionInfo.HasValue)
            {
                _logger.Information("Workflow suspended for signal: {WorkflowId} (Signal: {SignalName})",
                    definition.WorkflowId, executionResult.suspensionInfo.Value.SignalName);

                return new WorkflowResult<TContext>
                {
                    IsSuccess = false,
                    Context = context,
                    ErrorMessage = $"WAITING_FOR_SIGNAL:{executionResult.suspensionInfo.Value.SignalName}",
                    Metrics = finalMetrics,
                    ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
                };
            }

            // FIXED: Properly handle failures with error message propagation
            if (!executionResult.success)
            {
                _logger.Warning("Workflow execution failed: {WorkflowId} (Error: {ErrorMessage})",
                    definition.WorkflowId, executionResult.errorMessage);

                return new WorkflowResult<TContext>
                {
                    IsSuccess = false,
                    Context = context,
                    ErrorMessage = executionResult.errorMessage ?? "Workflow execution failed",
                    Exception = executionResult.exception,
                    Metrics = finalMetrics,
                    ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
                };
            }

            _logger.Information("Workflow execution completed: {WorkflowId} (Success: {Success}, Duration: {Duration})",
                definition.WorkflowId, executionResult.success, stopwatch.Elapsed);

            return new WorkflowResult<TContext>
            {
                IsSuccess = true,
                Context = context,
                Metrics = finalMetrics,
                ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.Warning("Workflow execution cancelled: {WorkflowId}", definition.WorkflowId);

            return new WorkflowResult<TContext>
            {
                IsSuccess = false,
                Context = context,
                ErrorMessage = "Workflow execution was cancelled",
                ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Workflow execution failed: {WorkflowId}", definition.WorkflowId);

            return new WorkflowResult<TContext>
            {
                IsSuccess = false,
                Context = context,
                ErrorMessage = ex.Message,
                Exception = ex,
                ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
            };
        }
    }

    /// <summary>
    /// CRITICAL FIX: Enhanced workflow graph execution that properly follows NextNodes
    /// and correctly propagates error messages from failed steps.
    /// </summary>
    private async ValueTask<WorkflowExecutionResult> ExecuteWorkflowGraphFixed<TContext>(
        IWorkflowNode<TContext> rootNode,
        TContext context,
        WorkflowExecutionContext<TContext> executionContext,
        CancellationToken cancellationToken) where TContext : class
    {
        var nodesToProcess = new Queue<IWorkflowNode<TContext>>();
        var processedNodes = new HashSet<string>();

        nodesToProcess.Enqueue(rootNode);
        _logger.Debug("Starting workflow execution with root node: {NodeId}", rootNode.NodeId);

        while (nodesToProcess.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentNode = nodesToProcess.Dequeue();

            // CRITICAL FIX: Allow nodes to be processed multiple times if they appear in different branches
            // but track processing for infinite loop detection
            var nodeKey = $"{currentNode.NodeId}_{currentNode.GetHashCode()}";

            if (processedNodes.Contains(nodeKey))
            {
                _logger.Debug("Skipping already processed node: {NodeId}", currentNode.NodeId);
                continue;
            }

            processedNodes.Add(nodeKey);
            _logger.Debug("Processing node: {NodeId} (Queue depth: {QueueDepth})", currentNode.NodeId,
                nodesToProcess.Count);

            var nodeResult = await ExecuteNodeWithRetryFixed(
                currentNode,
                context,
                executionContext,
                cancellationToken);

            // CRITICAL FIX: Check for suspension at node level
            if (!nodeResult.StepResult.IsSuccess)
            {
                if (IsSuspensionSignal(nodeResult.StepResult))
                {
                    var signalName = ExtractSignalName(nodeResult.StepResult);
                    _logger.Information("Node {NodeId} returned suspension signal '{SignalName}'",
                        currentNode.NodeId, signalName);

                    return new WorkflowExecutionResult
                    {
                        success = false,
                        suspensionInfo = new SuspensionInfo
                        {
                            SignalName = signalName,
                            Metadata = nodeResult.StepResult.Metadata,
                            NodeId = currentNode.NodeId
                        }
                    };
                }

                // FIXED: Properly capture and propagate error details from failed nodes
                var errorMessage = nodeResult.StepResult.ErrorMessage ?? $"Node '{currentNode.NodeId}' execution failed";
                _logger.Error("Node execution failed: {NodeId} - {Error}", currentNode.NodeId, errorMessage);

                return new WorkflowExecutionResult
                {
                    success = false,
                    errorMessage = errorMessage,
                    exception = nodeResult.StepResult.Exception
                };
            }

            // CRITICAL FIX: Properly enqueue ALL next nodes for continued execution
            _logger.Debug("Node {NodeId} completed successfully, found {NextNodeCount} next nodes",
                currentNode.NodeId, nodeResult.NextNodes.Count);

            foreach (var nextNode in nodeResult.NextNodes)
            {
                nodesToProcess.Enqueue(nextNode);
                _logger.Debug("Enqueued next node: {NodeId} -> {NextNodeId}", currentNode.NodeId, nextNode.NodeId);
            }

            // CRITICAL FIX: If no next nodes, check if queue is empty to determine completion
            if (nodeResult.NextNodes.Count == 0)
            {
                _logger.Debug("Node {NodeId} has no next nodes", currentNode.NodeId);

                if (nodesToProcess.Count == 0)
                {
                    _logger.Debug("No more nodes to process - workflow complete");
                    break;
                }
                else
                {
                    _logger.Debug("No next nodes for this branch, but {RemainingNodes} nodes still in queue",
                        nodesToProcess.Count);
                }
            }
        }

        _logger.Information("Workflow graph execution completed successfully");
        return new WorkflowExecutionResult { success = true };
    }

    /// <summary>
    /// FIXED: Execute node with retry, properly handling suspension signals
    /// </summary>
    private async ValueTask<NodeExecutionResult<TContext>> ExecuteNodeWithRetryFixed<TContext>(
        IWorkflowNode<TContext> node,
        TContext context,
        WorkflowExecutionContext<TContext> executionContext,
        CancellationToken cancellationToken) where TContext : class
    {
        var attempt = 0;
        var maxAttempts = executionContext.Options.MaxRetryAttempts + 1;
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            var stepTrace = new StepExecutionTrace
            {
                StepName = node.NodeId,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow,
                Result = StepResult.Success(),
                RetryAttempts = attempt,
                CorrelationId = executionContext.CorrelationId
            };

            try
            {
                _logger.Debug("Executing node {NodeId} (attempt {Attempt})", node.NodeId, attempt + 1);

                var result = await node.ExecuteAsync(context, executionContext.ServiceProvider, cancellationToken);

                stepTrace = stepTrace with { EndTime = DateTimeOffset.UtcNow, Result = result.StepResult };

                if (executionContext.Options.EnableExecutionTracing)
                {
                    executionContext.ExecutionTrace.Add(stepTrace);
                }

                // CRITICAL FIX: Don't retry suspension signals and propagate them immediately
                if (result.StepResult.IsSuccess)
                {
                    _logger.Debug("Node {NodeId} completed successfully with {NextNodeCount} next nodes",
                        node.NodeId, result.NextNodes.Count);
                    return result;
                }

                // CRITICAL FIX: Check for suspension signal and return immediately without retry
                if (IsSuspensionSignal(result.StepResult))
                {
                    _logger.Debug("Node {NodeId} returned suspension signal, propagating immediately", node.NodeId);
                    return result;
                }

                if (!result.StepResult.ShouldRetry)
                {
                    _logger.Debug("Node {NodeId} failed and should not retry: {ErrorMessage}",
                        node.NodeId, result.StepResult.ErrorMessage);
                    return result;
                }

                // Store the last result for potential retry
                lastException = result.StepResult.Exception;

                attempt++;
                if (attempt < maxAttempts)
                {
                    var delay = CalculateRetryDelay(attempt, executionContext.Options);
                    _logger.Debug("Node {NodeId} failed, retrying in {Delay}ms (attempt {Attempt}/{MaxAttempts})",
                        node.NodeId, delay.TotalMilliseconds, attempt + 1, maxAttempts);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    _logger.Warning("Node {NodeId} failed after {MaxAttempts} attempts: {ErrorMessage}",
                        node.NodeId, maxAttempts, result.StepResult.ErrorMessage);
                    return result; // Return the last failed result
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Node {NodeId} execution cancelled", node.NodeId);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.Warning(ex, "Node {NodeId} execution failed with exception (attempt {Attempt}/{MaxAttempts})",
                    node.NodeId, attempt + 1, maxAttempts);

                stepTrace = stepTrace with { EndTime = DateTimeOffset.UtcNow, Result = StepResult.Failure(ex) };

                if (executionContext.Options.EnableExecutionTracing)
                {
                    executionContext.ExecutionTrace.Add(stepTrace);
                }

                attempt++;
                if (attempt >= maxAttempts)
                {
                    return NodeExecutionResult<TContext>.Failure(StepResult.Failure(ex));
                }

                var delay = CalculateRetryDelay(attempt, executionContext.Options);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // FIXED: Provide detailed error message when all retries are exhausted
        var finalErrorMessage = lastException?.Message ?? $"Node '{node.NodeId}' failed after {maxAttempts} attempts";
        return NodeExecutionResult<TContext>.Failure(
            StepResult.Failure(finalErrorMessage, false, null));
    }

    /// <summary>
    /// Enhanced suspension signal detection
    /// </summary>
    private static bool IsSuspensionSignal(StepResult stepResult)
    {
        return !stepResult.IsSuccess &&
               stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Extract signal name from suspension signal
    /// </summary>
    private static string ExtractSignalName(StepResult stepResult)
    {
        if (stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true)
        {
            return stepResult.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
        }

        return "unknown_signal";
    }

    private static TimeSpan CalculateRetryDelay(int attemptNumber, WorkflowExecutionOptions options)
    {
        var delay = TimeSpan.FromMilliseconds(
            options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber));

        return delay > options.MaxRetryDelay ? options.MaxRetryDelay : delay;
    }

    private static Activity? CreateActivity(string workflowId, string correlationId)
    {
        var activity = new Activity("WorkflowExecution");
        activity.SetTag("workflow.id", workflowId);
        activity.SetTag("correlation.id", correlationId);
        return activity.Start();
    }

    /// <summary>
    /// FIXED: Enhanced execution result structure with proper error information
    /// </summary>
    private record struct WorkflowExecutionResult
    {
        public bool success { get; init; }
        public SuspensionInfo? suspensionInfo { get; init; }
        public string? errorMessage { get; init; }
        public Exception? exception { get; init; }
    }

    private record struct SuspensionInfo
    {
        public string SignalName { get; init; }
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }
        public string? NodeId { get; init; }
    }
}
