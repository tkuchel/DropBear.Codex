#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for hashing operations.
/// </summary>
public sealed record HashingError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HashingError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public HashingError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
