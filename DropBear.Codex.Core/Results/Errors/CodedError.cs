#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     An error with a specific error code for categorization.
/// </summary>
/// <remarks>
///     Use this error type when you need to classify errors by specific codes
///     for automated handling, routing, or internationalization purposes.
/// </remarks>
/// <example>
/// <code>
/// var error = CodedError.Create("INVALID_INPUT", "The provided email address is invalid");
/// 
/// // Or using constructor
/// var error = new CodedError("Email validation failed", "VAL_001");
/// </code>
/// </example>
public sealed record CodedError : ResultError
{
    /// <summary>
    ///     Initializes a new CodedError with a message and code.
    /// </summary>
    /// <param name="message">The error message describing what went wrong.</param>
    /// <param name="code">The error code for classification.</param>
    /// <exception cref="ArgumentException">Thrown when code is null or whitespace.</exception>
    public CodedError(string message, string code) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        Code = code;
        // Note: Message already set by base constructor - no need to set it again
    }

    /// <summary>
    ///     Creates a new CodedError with the specified code and message.
    /// </summary>
    /// <param name="code">The error code for classification.</param>
    /// <param name="message">The error message describing what went wrong.</param>
    /// <returns>A new <see cref="CodedError"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when code or message is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// var error = CodedError.Create("NOT_FOUND", "User not found");
    /// </code>
    /// </example>
    public static CodedError Create(string code, string message) => new(message, code);

    /// <summary>
    ///     Creates a CodedError from an exception, extracting the error code from the exception type.
    /// </summary>
    /// <param name="exception">The source exception.</param>
    /// <param name="customCode">Optional custom code. If not provided, uses the exception type name.</param>
    /// <returns>A new <see cref="CodedError"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when exception is null.</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Some operation
    /// }
    /// catch (Exception ex)
    /// {
    ///     var error = CodedError.FromException(ex, "CUSTOM_CODE");
    ///     return Result&lt;Data, CodedError&gt;.Failure(error);
    /// }
    /// </code>
    /// </example>
    public static CodedError FromException(Exception exception, string? customCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception, nameof(exception));

        var code = customCode ?? exception.GetType().Name.Replace("Exception", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        
        return new CodedError(exception.Message, code)
        {
            SourceException = exception,
            StackTrace = exception.StackTrace ?? Environment.StackTrace
        };
    }
}
