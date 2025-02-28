#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Represents an error that occurs during Excel export operations.
///     Extends the base ResultError class to work with the Result pattern.
/// </summary>
public sealed record ExportError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExportError" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that describes the export failure.</param>
    public ExportError(string message) : base(message) { }
}
