using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Metrics;

namespace DropBear.Codex.Workflow.Results;

/// <summary>
/// Represents the final result of a workflow execution with comprehensive diagnostics and metrics.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed record WorkflowResult<TContext> where TContext : class
{
    /// <summary>
    /// Gets a value indicating whether the workflow executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the final workflow context after execution.
    /// </summary>
    public TContext Context { get; init; } = default!;

    /// <summary>
    /// Gets the error message if the workflow failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the source exception that caused the failure, if any.
    /// </summary>
    public Exception? SourceException { get; init; }

    /// <summary>
    /// Gets a value indicating whether a source exception is present.
    /// </summary>
    public bool HasException => SourceException is not null;

    /// <summary>
    /// Gets the execution metrics for this workflow run.
    /// </summary>
    public WorkflowMetrics? Metrics { get; init; }

    /// <summary>
    /// Gets the detailed execution trace of all steps.
    /// </summary>
    public IReadOnlyList<StepExecutionTrace>? ExecutionTrace { get; init; }

    /// <summary>
    /// Gets the correlation ID for this workflow execution.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workflow was suspended (for persistent workflows).
    /// </summary>
    public bool IsSuspended { get; init; }

    /// <summary>
    /// Gets the signal name if the workflow was suspended waiting for a signal.
    /// </summary>
    public string? SuspendedSignalName { get; init; }

    /// <summary>
    /// Gets additional metadata about the suspension, if applicable.
    /// </summary>
    public IReadOnlyDictionary<string, object>? SuspensionMetadata { get; init; }

    /// <summary>
    /// Gets the list of compensation failures that occurred during workflow rollback, if any.
    /// Only populated when compensation is enabled and workflow fails.
    /// </summary>
    public IReadOnlyList<CompensationFailure>? CompensationFailures { get; init; }

    /// <summary>
    /// Gets a value indicating whether compensation was attempted and had failures.
    /// </summary>
    public bool HasCompensationFailures => CompensationFailures?.Count > 0;

    /// <summary>
    /// Gets the full exception message including inner exceptions.
    /// </summary>
    public string GetFullExceptionMessage()
    {
        if (SourceException is null)
            return ErrorMessage ?? string.Empty;

        var messages = new List<string>();
        var currentException = SourceException;

        while (currentException is not null)
        {
            messages.Add(currentException.Message);
            currentException = currentException.InnerException;
        }

        return string.Join(" -> ", messages);
    }

    /// <summary>
    /// Creates a successful workflow result.
    /// </summary>
    /// <param name="context">The final workflow context</param>
    /// <param name="metrics">Optional execution metrics</param>
    /// <param name="executionTrace">Optional execution trace</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>A successful workflow result</returns>
    public static WorkflowResult<TContext> Success(
        TContext context,
        WorkflowMetrics? metrics = null,
        IReadOnlyList<StepExecutionTrace>? executionTrace = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new WorkflowResult<TContext>
        {
            IsSuccess = true,
            Context = context,
            Metrics = metrics,
            ExecutionTrace = executionTrace,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failed workflow result.
    /// </summary>
    /// <param name="context">The final workflow context</param>
    /// <param name="errorMessage">The error message</param>
    /// <param name="metrics">Optional execution metrics</param>
    /// <param name="executionTrace">Optional execution trace</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="compensationFailures">Optional list of compensation failures during rollback</param>
    /// <returns>A failed workflow result</returns>
    public static WorkflowResult<TContext> Failure(
        TContext context,
        string errorMessage,
        WorkflowMetrics? metrics = null,
        IReadOnlyList<StepExecutionTrace>? executionTrace = null,
        string? correlationId = null,
        IReadOnlyList<CompensationFailure>? compensationFailures = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new WorkflowResult<TContext>
        {
            IsSuccess = false,
            Context = context,
            ErrorMessage = errorMessage,
            Metrics = metrics,
            ExecutionTrace = executionTrace,
            CorrelationId = correlationId,
            CompensationFailures = compensationFailures
        };
    }

    /// <summary>
    /// Creates a failed workflow result from an exception with full exception preservation.
    /// </summary>
    /// <param name="context">The final workflow context</param>
    /// <param name="exception">The exception that caused the failure</param>
    /// <param name="metrics">Optional execution metrics</param>
    /// <param name="executionTrace">Optional execution trace</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="compensationFailures">Optional list of compensation failures during rollback</param>
    /// <returns>A failed workflow result with exception details</returns>
    public static WorkflowResult<TContext> Failure(
        TContext context,
        Exception exception,
        WorkflowMetrics? metrics = null,
        IReadOnlyList<StepExecutionTrace>? executionTrace = null,
        string? correlationId = null,
        IReadOnlyList<CompensationFailure>? compensationFailures = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exception);

        return new WorkflowResult<TContext>
        {
            IsSuccess = false,
            Context = context,
            ErrorMessage = exception.Message,
            SourceException = exception,
            Metrics = metrics,
            ExecutionTrace = executionTrace,
            CorrelationId = correlationId,
            CompensationFailures = compensationFailures
        };
    }

    /// <summary>
    /// Creates a failed workflow result from a ResultError (integration with DropBear.Codex.Core).
    /// </summary>
    /// <param name="context">The final workflow context</param>
    /// <param name="error">The result error from DropBear.Codex.Core</param>
    /// <param name="metrics">Optional execution metrics</param>
    /// <param name="executionTrace">Optional execution trace</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>A failed workflow result with error details</returns>
    public static WorkflowResult<TContext> FromError(
        TContext context,
        ResultError error,
        WorkflowMetrics? metrics = null,
        IReadOnlyList<StepExecutionTrace>? executionTrace = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        return new WorkflowResult<TContext>
        {
            IsSuccess = false,
            Context = context,
            ErrorMessage = error.Message,
            SourceException = error.SourceException,
            Metrics = metrics,
            ExecutionTrace = executionTrace,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a suspended workflow result for persistent workflows.
    /// </summary>
    /// <param name="context">The current workflow context</param>
    /// <param name="signalName">The signal name the workflow is waiting for</param>
    /// <param name="suspensionMetadata">Optional metadata about the suspension</param>
    /// <param name="metrics">Optional execution metrics</param>
    /// <param name="executionTrace">Optional execution trace</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>A suspended workflow result</returns>
    public static WorkflowResult<TContext> Suspended(
        TContext context,
        string signalName,
        IReadOnlyDictionary<string, object>? suspensionMetadata = null,
        WorkflowMetrics? metrics = null,
        IReadOnlyList<StepExecutionTrace>? executionTrace = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        return new WorkflowResult<TContext>
        {
            IsSuccess = false,
            Context = context,
            IsSuspended = true,
            SuspendedSignalName = signalName,
            SuspensionMetadata = suspensionMetadata,
            ErrorMessage = $"Workflow suspended, waiting for signal: {signalName}",
            Metrics = metrics,
            ExecutionTrace = executionTrace,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Adds exception information to this result.
    /// </summary>
    /// <param name="exception">The exception to attach</param>
    /// <returns>A new workflow result with the exception attached</returns>
    public WorkflowResult<TContext> WithException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return this with
        {
            SourceException = exception,
            ErrorMessage = ErrorMessage ?? exception.Message
        };
    }

    #region Result Pattern Interoperability

    /// <summary>
    /// Converts this WorkflowResult to Core's Result pattern using OperationError.
    /// This enables integration with Result-based pipelines and APIs.
    /// </summary>
    /// <returns>A Result containing the context or an OperationError</returns>
    public Result<TContext, OperationError> ToResult()
    {
        if (IsSuccess)
        {
            return Result<TContext, OperationError>.Success(Context);
        }

        var error = OperationError.ForOperation(
            "WorkflowExecution",
            ErrorMessage ?? "Workflow execution failed"
        );

        // Add workflow-specific metadata
        if (CorrelationId is not null)
        {
            error = (OperationError)error.WithMetadata("CorrelationId", CorrelationId);
        }

        if (Metrics is not null)
        {
            error = (OperationError)error.WithMetadata("Metrics", Metrics);
        }

        if (IsSuspended)
        {
            error = (OperationError)error.WithMetadata("IsSuspended", true);
            if (SuspendedSignalName is not null)
            {
                error = (OperationError)error.WithMetadata("SuspendedSignalName", SuspendedSignalName);
            }
        }

        // Preserve exception if present
        if (SourceException is not null)
        {
            return Result<TContext, OperationError>.Failure(error, SourceException);
        }

        return Result<TContext, OperationError>.Failure(error);
    }

    /// <summary>
    /// Converts this WorkflowResult to Core's Result pattern with a custom error type.
    /// Use this when you need to map workflow errors to a specific error domain.
    /// </summary>
    /// <typeparam name="TError">The target error type</typeparam>
    /// <param name="errorMapper">Function to map from WorkflowResult to TError</param>
    /// <returns>A Result containing the context or a custom error</returns>
    public Result<TContext, TError> ToResult<TError>(Func<WorkflowResult<TContext>, TError> errorMapper)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorMapper);

        if (IsSuccess)
        {
            return Result<TContext, TError>.Success(Context);
        }

        var error = errorMapper(this);
        return SourceException is not null
            ? Result<TContext, TError>.Failure(error, SourceException)
            : Result<TContext, TError>.Failure(error);
    }

    #endregion
}
