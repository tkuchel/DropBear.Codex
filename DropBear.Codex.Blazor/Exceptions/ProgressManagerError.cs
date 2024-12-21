#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Exceptions;

/// <summary>
///     Represents errors that can occur during progress management operations.
/// </summary>
public sealed record ProgressManagerError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the ProgressManagerError class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">Optional exception that caused the error.</param>
    public ProgressManagerError(string message, Exception? exception = null) : base(message)
    {
        Exception = exception;
    }

    /// <summary>
    ///     Gets the exception that caused the error, if any.
    /// </summary>
    public Exception? Exception { get; }
}
