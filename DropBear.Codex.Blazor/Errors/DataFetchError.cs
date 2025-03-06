using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that can occur during data fetching operations.
/// </summary>
public sealed record DataFetchError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFetchError"/> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="operationName">The name of the operation that failed.</param>
    public DataFetchError(string message, string operationName)
        : base(message)
    {
        OperationName = operationName;
    }

    /// <summary>
    ///     Gets the name of the operation that failed.
    /// </summary>
    public string OperationName { get; }
}
