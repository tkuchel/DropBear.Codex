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
    public ComponentError(string message) : base(message)
    {
    }
}
