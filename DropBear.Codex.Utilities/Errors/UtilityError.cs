#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Represents errors that occur in utility operations such as rate limiting,
///     validation, and general utility functions.
/// </summary>
public sealed record UtilityError : ResultError
{
    private UtilityError(string message, string? code = null) : base(message)
    {
        Code = code;
    }

    /// <summary>
    ///     Gets the error code.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    ///     Gets the time to wait before retrying (for rate limit errors).
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    ///     Creates a rate limit exceeded error.
    /// </summary>
    public static UtilityError RateLimitExceeded(string message, TimeSpan retryAfter)
    {
        return new UtilityError(message, "RATE_LIMIT_EXCEEDED")
        {
            RetryAfter = retryAfter,
            Severity = ErrorSeverity.Medium,
            Category = ErrorCategory.Technical
        };
    }

    /// <summary>
    ///     Creates a validation error.
    /// </summary>
    public static UtilityError ValidationFailed(string message)
    {
        return new UtilityError(message, "VALIDATION_FAILED")
        {
            Severity = ErrorSeverity.Low,
            Category = ErrorCategory.Validation
        };
    }

    /// <summary>
    ///     Creates an operation failed error.
    /// </summary>
    public static UtilityError OperationFailed(string message)
    {
        return new UtilityError(message, "OPERATION_FAILED")
        {
            Severity = ErrorSeverity.Medium,
            Category = ErrorCategory.Technical
        };
    }

    /// <summary>
    ///     Creates a configuration error.
    /// </summary>
    public static UtilityError ConfigurationError(string message)
    {
        return new UtilityError(message, "CONFIGURATION_ERROR")
        {
            Severity = ErrorSeverity.High,
            Category = ErrorCategory.Technical
        };
    }

    /// <summary>
    ///     Creates a resource not found error.
    /// </summary>
    public static UtilityError NotFound(string resourceName)
    {
        return new UtilityError($"Resource '{resourceName}' not found", "NOT_FOUND")
        {
            Severity = ErrorSeverity.Low,
            Category = ErrorCategory.General
        };
    }
}
