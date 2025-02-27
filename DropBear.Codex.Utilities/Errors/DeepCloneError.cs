#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains error details for deep cloning operations.
/// </summary>
public sealed record DeepCloneError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DeepCloneError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DeepCloneError(string message) : base(message) { }
}
