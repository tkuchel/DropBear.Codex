using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Metrics;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// Main workflow execution engine with retry logic, metrics collection, and error handling.
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

            var success = await ExecuteWorkflowGraphAsync(
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
    /// Executes the workflow graph using breadth-first traversal.
    /// </summary>
    private async ValueTask<bool> ExecuteWorkflowGraphAsync<TContext>(
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

            var nodeResult = await ExecuteNodeWithRetryAsync(
                currentNode,
                context,
                executionContext,
                cancellationToken);

            if (!nodeResult.StepResult.IsSuccess)
            {
                _logger.Error("Node execution failed: {NodeId} - {Error}",
                    currentNode.NodeId, nodeResult.StepResult.ErrorMessage);
                return false;
            }

            // Enqueue next nodes for processing
            foreach (var nextNode in nodeResult.NextNodes)
            {
                nodesToProcess.Enqueue(nextNode);
            }
        }

        return true;
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

                if (result.StepResult.IsSuccess || !result.StepResult.ShouldRetry)
                {
                    return result;
                }

                attempt++;
                if (attempt < maxAttempts)
                {
                    var delay = CalculateRetryDelay(attempt, executionContext.Options);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
}


