#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Interface for the execution engine factory
/// </summary>
public interface IExecutionEngineFactory
{
    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" />.
    /// </summary>
    Result<ExecutionEngine, ExecutionEngineError> CreateExecutionEngine(Guid channelId);
}
