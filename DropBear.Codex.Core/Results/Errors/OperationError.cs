#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     An error that occurred during an operation.
///     Includes operation name for better context.
/// </summary>
public sealed record OperationError : ResultError
{
    /// <summary>
    ///     Initializes a new OperationError.
    /// </summary>
    public OperationError(string message) : base(message)
    {
        Message = message;
    }

    /// <summary>
    ///     Gets or sets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    ///     Creates a new OperationError for a specific operation.
    /// </summary>
    public static OperationError ForOperation(string operationName, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationError(message) { OperationName = operationName };
    }

    /// <summary>
    ///     Returns a string representation including the operation name.
    /// </summary>
    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(OperationName))
        {
            return $"[{OperationName}] {base.ToString()}";
        }

        return base.ToString();
    }
}
