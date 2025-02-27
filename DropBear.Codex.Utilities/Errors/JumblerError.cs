#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains error information for password jumbling operations.
/// </summary>
public sealed record JumblerError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="JumblerError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JumblerError(string message) : base(message) { }
}
