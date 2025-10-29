#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for task-related operations.
/// </summary>
public sealed record TaskError : ResultError
{
    public TaskError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
