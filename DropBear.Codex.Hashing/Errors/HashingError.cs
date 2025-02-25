#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Hashing.Errors;

/// <summary>
///     Base error type for all hashing-related operations.
/// </summary>
public abstract record HashingError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="HashingError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    protected HashingError(string message, DateTime? timestamp = null)
        : base(message, timestamp)
    {
    }
}
