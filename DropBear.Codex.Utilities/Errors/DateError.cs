#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for date-related operations.
/// </summary>
public sealed record DateError : ResultError
{
    public DateError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
