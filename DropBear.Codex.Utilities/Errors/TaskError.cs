#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for task-related operations.
/// </summary>
public sealed record TaskError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public TaskError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
