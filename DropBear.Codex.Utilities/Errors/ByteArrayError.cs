#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error for byte array operations.
/// </summary>
public sealed record ByteArrayError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ByteArrayError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public ByteArrayError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
