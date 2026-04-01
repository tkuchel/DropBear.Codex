#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for string operations.
/// </summary>
public sealed record StringError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StringError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public StringError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
