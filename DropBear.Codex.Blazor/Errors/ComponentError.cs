using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that can occur during section component operations.
/// </summary>
public sealed record ComponentError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ComponentError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="timestamp">Optional timestamp for the error. Defaults to UTC now.</param>
    public ComponentError(string message, DateTime? timestamp = null) : base(message, timestamp)
    {
    }
}
