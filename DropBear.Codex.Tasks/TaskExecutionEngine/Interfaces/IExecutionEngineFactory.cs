#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Interface for the execution engine factory, providing a flexible method for creating execution engines.
/// </summary>
public interface IExecutionEngineFactory
{
    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" />.
    /// </summary>
    /// <param name="channelId">The channel ID for keyed pub/sub. Optional for flexible configurations.</param>
    /// <returns>A result containing the execution engine or an error if creation fails.</returns>
    Result<ExecutionEngine, ExecutionEngineError> CreateExecutionEngine(Guid channelId);
}
