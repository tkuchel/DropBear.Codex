#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error for byte array operations.
/// </summary>
public sealed record ByteArrayError : ResultError
{
    public ByteArrayError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
