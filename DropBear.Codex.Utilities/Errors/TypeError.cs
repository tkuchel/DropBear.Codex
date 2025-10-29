#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for type-related operations.
/// </summary>
public sealed record TypeError : ResultError
{
    public TypeError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
