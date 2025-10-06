#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during JavaScript initialization operations.
/// </summary>
public sealed record JsInitializationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="JsInitializationError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JsInitializationError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets additional details about the error.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    ///     Creates an error for module not found.
    /// </summary>
    /// <param name="moduleName">The name of the module that was not found.</param>
    /// <returns>A new <see cref="JsInitializationError" /> instance.</returns>
    public static JsInitializationError ModuleNotFound(string moduleName)
    {
        return new JsInitializationError($"Module '{moduleName}' not found");
    }

    /// <summary>
    ///     Creates an error for initialization failure.
    /// </summary>
    /// <param name="moduleName">The name of the module that failed to initialize.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="JsInitializationError" /> instance.</returns>
    public static JsInitializationError InitializationFailed(string moduleName, string details)
    {
        return new JsInitializationError($"Failed to initialize module '{moduleName}'") { Details = details };
    }

    /// <summary>
    ///     Creates an error for timeout during initialization.
    /// </summary>
    /// <param name="moduleName">The name of the module that timed out.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>A new <see cref="JsInitializationError" /> instance.</returns>
    public static JsInitializationError Timeout(string moduleName, double timeoutSeconds)
    {
        return new JsInitializationError(
            $"Initialization of module '{moduleName}' timed out after {timeoutSeconds:F1}s");
    }

    /// <summary>
    ///     Creates an error for a general operation failure.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="JsInitializationError" /> instance.</returns>
    public static JsInitializationError OperationFailed(string operation, string details)
    {
        return new JsInitializationError($"Operation '{operation}' failed") { Details = details };
    }
}
