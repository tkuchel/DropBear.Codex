#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains error information for debounce operations.
/// </summary>
public sealed record DebounceError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DebounceError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DebounceError(string message) : base(message) { }
}
