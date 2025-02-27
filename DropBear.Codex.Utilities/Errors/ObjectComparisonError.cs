#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Contains possible errors that can occur during object comparison operations.
/// </summary>
public sealed record ObjectComparisonError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ObjectComparisonError" /> record.
    /// </summary>
    /// <param name="message">The error message describing the comparison failure.</param>
    public ObjectComparisonError(string message) : base(message) { }
}
