#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains error information for simple obfuscation operations.
/// </summary>
public sealed record ObfuscationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ObfuscationError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ObfuscationError(string message) : base(message) { }
}
