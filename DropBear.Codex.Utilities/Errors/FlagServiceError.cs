using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains error information for dynamic flag operations.
/// </summary>
public sealed record FlagServiceError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FlagServiceError"/> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FlagServiceError(string message) : base(message) { }
}
