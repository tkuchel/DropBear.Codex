#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for time conversion operations.
/// </summary>
public sealed record TimeError : ResultError
{
    public TimeError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
