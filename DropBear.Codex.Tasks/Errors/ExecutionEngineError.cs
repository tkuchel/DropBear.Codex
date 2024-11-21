#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Represents errors that can occur in the ExecutionEngine
/// </summary>
public sealed record ExecutionEngineError : ResultError
{
    public ExecutionEngineError(string message, Exception? exception = null)
        : base(message)
    {
        Exception = exception;
    }

    public Exception? Exception { get; }
}
