using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Compatibility;

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Defines a factory for creating instances of <see cref="ExecutionEngine" />.
/// </summary>
public interface IExecutionEngineFactory
{
    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" /> with the specified channel ID.
    /// </summary>
    /// <param name="channelId">The channel ID associated with the execution engine.</param>
    /// <returns>A ResultT containing a new instance of <see cref="ExecutionEngine" />.</returns>
    Result<ExecutionEngine> CreateExecutionEngine(Guid channelId);
}
