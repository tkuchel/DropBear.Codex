#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for hashing operations.
/// </summary>
public sealed record HashingError : ResultError
{
    public HashingError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
