#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Common;

#endregion

namespace DropBear.Codex.Workflow.Results;

/// <summary>
///     Represents the result of a workflow step execution.
/// </summary>
public sealed class StepResult : Result<Unit, ResultError>
{
    private StepResult(
        ResultState state,
        Unit value,
        ResultError? error,
        bool shouldRetry,
        IReadOnlyDictionary<string, object>? metadata)
        : base(value, state, error)
    {
        ShouldRetry = shouldRetry;
        Metadata = metadata;

        if (state == ResultState.Success)
        {
            Value = value;
        }
        else if (error != null)
        {
            Error = error;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this step should be retried on failure.
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    ///     Gets optional metadata associated with this step execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    ///     Creates a successful step result.
    /// </summary>
    public static StepResult Success(IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new StepResult(
            ResultState.Success,
            Unit.Value,
            null,
            false,
            metadata);
    }

    /// <summary>
    ///     Creates a failed step result with an error message.
    /// </summary>
    public static StepResult Failure(
        string errorMessage,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (errorMessage.Length > WorkflowConstants.Limits.MaxErrorMessageLength)
        {
            errorMessage = errorMessage[..WorkflowConstants.Limits.MaxErrorMessageLength];
        }

        var error = new SimpleError(errorMessage);

        return new StepResult(
            ResultState.Failure,
            default,
            error,
            shouldRetry,
            metadata);
    }

    /// <summary>
    ///     Creates a failed step result from an exception with full exception preservation.
    /// </summary>
    public static StepResult Failure(
        Exception exception,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = SimpleError.FromException(exception);

        return new StepResult(
            ResultState.Failure,
            default,
            error,
            shouldRetry,
            metadata);
    }

    /// <summary>
    ///     Creates a failed step result from a ResultError.
    /// </summary>
    public static StepResult FromError(
        ResultError error,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new StepResult(
            ResultState.Failure,
            default,
            error,
            shouldRetry,
            metadata);
    }

    /// <summary>
    ///     Creates a suspension result that instructs the workflow to pause and wait for an external signal.
    /// </summary>
    public static StepResult Suspend(
        string signalName,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        if (signalName.Length > WorkflowConstants.Limits.MaxSignalNameLength)
        {
            throw new ArgumentException(
                $"Signal name cannot exceed {WorkflowConstants.Limits.MaxSignalNameLength} characters.",
                nameof(signalName));
        }

        string suspensionMessage = WorkflowConstants.Signals.CreateSuspensionMessage(signalName);
        var error = new SimpleError(suspensionMessage);

        Dictionary<string, object> suspensionMetadata = metadata != null
            ? new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        suspensionMetadata[WorkflowConstants.MetadataKeys.Suspension] = true;
        suspensionMetadata[WorkflowConstants.MetadataKeys.SignalName] = signalName;

        return new StepResult(
            ResultState.Failure,
            default,
            error,
            false,
            suspensionMetadata);
    }
}
