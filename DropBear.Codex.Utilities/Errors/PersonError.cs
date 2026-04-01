#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for person-related operations.
/// </summary>
public sealed record PersonError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PersonError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public PersonError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
