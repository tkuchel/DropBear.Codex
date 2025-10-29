#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents an error that occurred during JavaScript interop operations.
///     Enhanced for .NET 9 with improved error context and performance.
/// </summary>
public sealed record JsInteropError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="JsInteropError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JsInteropError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    ///     Gets or sets the calling method name.
    /// </summary>
    public string? Caller { get; init; }

    /// <summary>
    ///     Creates an error for a JavaScript invocation failure.
    /// </summary>
    /// <param name="operationName">The name of the operation that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <param name="caller">The calling method name.</param>
    /// <returns>A new <see cref="JsInteropError"/> instance.</returns>
    public static JsInteropError InvocationFailed(
        string operationName,
        string details,
        [CallerMemberName] string? caller = null)
    {
        return new JsInteropError($"JavaScript invocation '{operationName}' failed: {details}")
        {
            OperationName = operationName,
            Caller = caller
        };
    }

    /// <summary>
    ///     Creates an error for a module not found scenario.
    /// </summary>
    /// <param name="moduleName">The name of the module that was not found.</param>
    /// <returns>A new <see cref="JsInteropError"/> instance.</returns>
    public static JsInteropError ModuleNotFound(string moduleName)
    {
        return new JsInteropError($"JavaScript module '{moduleName}' not found");
    }

    /// <summary>
    ///     Creates an error for a timeout scenario.
    /// </summary>
    /// <param name="operationName">The name of the operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>A new <see cref="JsInteropError"/> instance.</returns>
    public static JsInteropError Timeout(string operationName, double timeoutSeconds)
    {
        return new JsInteropError(
            $"JavaScript operation '{operationName}' timed out after {timeoutSeconds:F1}s")
        {
            OperationName = operationName
        };
    }

    /// <summary>
    ///     Creates an error for a general operation failure.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="JsInteropError"/> instance.</returns>
    public static JsInteropError OperationFailed(string operation, string details)
    {
        return new JsInteropError($"JavaScript operation '{operation}' failed: {details}")
        {
            OperationName = operation
        };
    }
}
