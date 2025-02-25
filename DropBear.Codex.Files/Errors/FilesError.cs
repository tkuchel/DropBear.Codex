#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Files.Errors;

/// <summary>
///     Base error type for all file-related operations in the DropBear.Codex.Files library.
/// </summary>
public abstract record FilesError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="FilesError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    protected FilesError(string message, DateTime? timestamp = null)
        : base(message, timestamp)
    {
    }
}
