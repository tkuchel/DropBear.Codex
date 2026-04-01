#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for read-only conversion failures.
/// </summary>
public sealed record ReadOnlyConversionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ReadOnlyConversionError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    public ReadOnlyConversionError(string message, Exception? exception = null)
        : base(message)
    {
    }
}
