#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for person-related operations.
/// </summary>
public sealed record PersonError : ResultError
{
    public PersonError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
