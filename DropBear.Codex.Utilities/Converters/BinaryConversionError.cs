#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Converters;

/// <summary>
///     Contains error details for binary and hex conversion operations.
/// </summary>
public sealed record BinaryConversionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BinaryConversionError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public BinaryConversionError(string message) : base(message) { }
}
