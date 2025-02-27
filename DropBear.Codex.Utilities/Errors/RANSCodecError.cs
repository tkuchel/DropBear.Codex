#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Represents errors that can occur during rANS codec operations.
/// </summary>
public sealed record RANSCodecError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RANSCodecError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RANSCodecError(string message) : base(message) { }
}
