#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.AccessCodes;

/// <summary>
///     Represents specific errors that can occur during time-based code operations.
/// </summary>
public sealed record TimeBasedCodeError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeBasedCodeError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TimeBasedCodeError(string message) : base(message) { }
}
