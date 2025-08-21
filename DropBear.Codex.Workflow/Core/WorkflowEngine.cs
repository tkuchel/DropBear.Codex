using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Metrics;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// FIXED: Main workflow execution engine with better signal detection for workflow suspension.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new workflow engine instance.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    public WorkflowEngine(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = LoggerFactory.Logger.ForContext<WorkflowEngine>() ?? throw new ArgumentNullException(nameof(_logger));
    }

    /// <inheritdoc />
    public ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return ExecuteAsync(definition, context, WorkflowExecutionOptions.Default, cancellationToken);
    }

    /// <inheritdoc />
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
            // Apply workflow-level timeout if specified
            using var workflowCts = definition.WorkflowTimeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (workflowCts is not null)
            {
                workflowCts.CancelAfter(definition.WorkflowTimeout.Value);
            }

            var effectiveCancellationToken = workflowCts?.Token ?? cancellationToken;

            // Build the workflow execution graph
            var rootNode = definition.BuildWorkflow();
            if (rootNode is null)
            {
                throw new InvalidOperationException($"Workflow '{definition.WorkflowId}' returned null root node");
            }

            // Execute the workflow graph
            var executionContext = new WorkflowExecutionContext<TContext>
            {
                Options = options,
                CorrelationId = correlationId,
                ExecutionTrace = executionTrace,
                ServiceProvider = _serviceProvider,
                Logger = _logger
            };

            var (success, suspensionInfo) = await ExecuteWorkflowGraphAsync(
                rootNode,
                context,
                executionContext,
                effectiveCancellationToken);

            stopwatch.Stop();

            // Build final metrics
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
            if (suspensionInfo.HasValue)
            {
                _logger.Information("Workflow suspended for signal: {WorkflowId} (Signal: {SignalName})",
                    definition.WorkflowId, suspensionInfo.Value.SignalName);

                return new WorkflowResult<TContext>
                {
                    IsSuccess = false, // Suspended workflows return false but with specific error message
                    Context = context,
                    ErrorMessage = $"WAITING_FOR_SIGNAL:{suspensionInfo.Value.SignalName}",
                    Metrics = finalMetrics,
                    ExecutionTrace = options.EnableExecutionTracing ? executionTrace.AsReadOnly() : null
                };
            }

            _logger.Information("Workflow execution completed: {WorkflowId} (Success: {Success}, Duration: {Duration})",
                definition.WorkflowId, success, stopwatch.Elapsed);

            return new WorkflowResult<TContext>
            {
                IsSuccess = success,
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
    /// FIXED: Executes the workflow graph using breadth-first traversal with better suspension detection.
    /// </summary>
    private async ValueTask<(bool success, SuspensionInfo? suspensionInfo)> ExecuteWorkflowGraphAsync<TContext>(
        IWorkflowNode<TContext> rootNode,
        TContext context,
        WorkflowExecutionContext<TContext> executionContext,
        CancellationToken cancellationToken) where TContext : class
    {
        var nodesToProcess = new Queue<IWorkflowNode<TContext>>();
        var processedNodes = new HashSet<string>();

        nodesToProcess.Enqueue(rootNode);

        while (nodesToProcess.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentNode = nodesToProcess.Dequeue();

            // Prevent infinite loops by tracking processed nodes
            if (!processedNodes.Add(currentNode.NodeId))
            {
                _logger.Warning("Skipping already processed node: {NodeId}", currentNode.NodeId);
                continue;
            }

            _logger.Debug("Processing node: {NodeId}", currentNode.NodeId);

            var nodeResult = await ExecuteNodeWithRetryAsync(
                currentNode,
                context,
                executionContext,
                cancellationToken);

            // FIXED: Enhanced suspension signal detection
            if (!nodeResult.StepResult.IsSuccess)
            {
                // Check if this is a suspension signal
                if (IsSuspensionSignal(nodeResult.StepResult))
                {
                    var signalName = ExtractSignalName(nodeResult.StepResult);

                    _logger.Information("Workflow suspended at node {NodeId} for signal '{SignalName}'",
                        currentNode.NodeId, signalName);

                    return (false, new SuspensionInfo
                    {
                        SignalName = signalName,
                        Metadata = nodeResult.StepResult.Metadata,
                        NodeId = currentNode.NodeId
                    });
                }

                _logger.Error("Node execution failed: {NodeId} - {Error}",
                    currentNode.NodeId, nodeResult.StepResult.ErrorMessage);
                return (false, null);
            }

            // Enqueue next nodes for processing
            foreach (var nextNode in nodeResult.NextNodes)
            {
                nodesToProcess.Enqueue(nextNode);
                _logger.Debug("Enqueued next node: {NodeId}", nextNode.NodeId);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// FIXED: Enhanced method to detect suspension signals
    /// </summary>
    private static bool IsSuspensionSignal(StepResult stepResult)
    {
        return !stepResult.IsSuccess &&
               stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// FIXED: Enhanced method to extract signal name from suspension signals
    /// </summary>
    private static string ExtractSignalName(StepResult stepResult)
    {
        if (stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true)
        {
            return stepResult.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
        }
        return "unknown_signal";
    }

    /// <summary>
    /// Executes a single node with retry logic.
    /// </summary>
    private async ValueTask<NodeExecutionResult<TContext>> ExecuteNodeWithRetryAsync<TContext>(
        IWorkflowNode<TContext> node,
        TContext context,
        WorkflowExecutionContext<TContext> executionContext,
        CancellationToken cancellationToken) where TContext : class
    {
        var attempt = 0;
        var maxAttempts = executionContext.Options.MaxRetryAttempts + 1;

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

                stepTrace = stepTrace with
                {
                    EndTime = DateTimeOffset.UtcNow,
                    Result = result.StepResult
                };

                if (executionContext.Options.EnableExecutionTracing)
                {
                    executionContext.ExecutionTrace.Add(stepTrace);
                }

                // FIXED: Don't retry suspension signals - they are not failures
                if (result.StepResult.IsSuccess || IsSuspensionSignal(result.StepResult))
                {
                    _logger.Debug("Node {NodeId} completed successfully or suspended", node.NodeId);
                    return result;
                }

                if (!result.StepResult.ShouldRetry)
                {
                    _logger.Debug("Node {NodeId} failed and should not retry", node.NodeId);
                    return result;
                }

                attempt++;
                if (attempt < maxAttempts)
                {
                    var delay = CalculateRetryDelay(attempt, executionContext.Options);
                    _logger.Debug("Node {NodeId} failed, retrying in {Delay}ms", node.NodeId, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Node {NodeId} execution cancelled", node.NodeId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Node {NodeId} execution failed with exception (attempt {Attempt})", node.NodeId, attempt + 1);

                stepTrace = stepTrace with
                {
                    EndTime = DateTimeOffset.UtcNow,
                    Result = StepResult.Failure(ex)
                };

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

        return NodeExecutionResult<TContext>.Failure(
            StepResult.Failure($"Node '{node.NodeId}' failed after {maxAttempts} attempts"));
    }

    /// <summary>
    /// Calculates the delay for retry attempts using exponential backoff.
    /// </summary>
    private static TimeSpan CalculateRetryDelay(int attemptNumber, WorkflowExecutionOptions options)
    {
        var delay = TimeSpan.FromMilliseconds(
            options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber));

        return delay > options.MaxRetryDelay ? options.MaxRetryDelay : delay;
    }

    /// <summary>
    /// Creates a diagnostic activity for workflow execution.
    /// </summary>
    private static Activity? CreateActivity(string workflowId, string correlationId)
    {
        var activity = new Activity("WorkflowExecution");
        activity.SetTag("workflow.id", workflowId);
        activity.SetTag("correlation.id", correlationId);
        return activity.Start();
    }

    /// <summary>
    /// FIXED: Enhanced suspension information with additional context
    /// </summary>
    private record struct SuspensionInfo
    {
        public string SignalName { get; init; }
        public IReadOnlyDictionary<string, object>? Metadata { get; init; }
        public string? NodeId { get; init; }
    }
}
