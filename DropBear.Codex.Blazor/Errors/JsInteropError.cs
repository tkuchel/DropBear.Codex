#region

using DropBear.Codex.Core.Results.Base;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during JavaScript interoperation.
/// </summary>
public sealed record JsInteropError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="JsInteropError" /> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JsInteropError(string message) : base(message) { }

    /// <summary>
    ///     Initializes a new instance of <see cref="JsInteropError" /> with a message and underlying exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The underlying exception.</param>
    public JsInteropError(string message, Exception exception) : base(message)
    {
        Exception = exception;
    }

    /// <summary>
    ///     Gets the underlying exception, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets whether the error was due to a disconnection.
    /// </summary>
    public bool IsDisconnection => Exception is JSDisconnectedException
        or TaskCanceledException or ObjectDisposedException;
}
