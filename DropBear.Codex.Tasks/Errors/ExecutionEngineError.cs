#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.TaskExecutionEngine;

#endregion

namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Represents errors that can occur within the <see cref="ExecutionEngine" />.
/// </summary>
public sealed record ExecutionEngineError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ExecutionEngineError" />.
    /// </summary>
    /// <param name="message">A descriptive message indicating what went wrong.</param>
    /// <param name="exception">An optional underlying exception.</param>
    public ExecutionEngineError(string message, Exception? exception = null)
        : base(message)
    {
        Exception = exception;
    }

    /// <summary>
    ///     The underlying exception, if any.
    /// </summary>
    public Exception? Exception { get; }
}
