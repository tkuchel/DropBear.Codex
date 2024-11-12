#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur during database operations
/// </summary>
public sealed record DatabaseError : ResultError
{
    #region Enums

    /// <summary>
    ///     Represents the severity level of a database error
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        ///     Warning level issues that don't prevent operation completion
        /// </summary>
        Warning,

        /// <summary>
        ///     Error level issues that prevent operation completion but are recoverable
        /// </summary>
        Error,

        /// <summary>
        ///     Critical issues that require immediate attention
        /// </summary>
        Critical
    }

    #endregion

    /// <summary>
    ///     Default error code for unknown errors
    /// </summary>
    public const string UnknownErrorCode = "UNKNOWN";

    /// <summary>
    ///     Default operation name for unknown operations
    /// </summary>
    public const string UnknownOperation = "Unknown";

    #region Private Methods

    private static string FormatErrorMessage(string operation, string message, string errorCode)
    {
        var severity = string.Equals(errorCode, UnknownErrorCode, StringComparison.Ordinal)
            ? "[Unknown Error]"
            : $"[{errorCode}]";
        return $"Database operation '{operation}' failed {severity}: {message}";
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new DatabaseError with a simple message
    /// </summary>
    /// <param name="message">The error message</param>
    public DatabaseError(string message)
        : this(UnknownOperation, message)
    {
    }

    /// <summary>
    ///     Creates a new DatabaseError with detailed information
    /// </summary>
    /// <param name="operation">The database operation that failed</param>
    /// <param name="message">The error message</param>
    /// <param name="errorCode">The specific error code</param>
    /// <param name="severity">The error severity</param>
    /// <exception cref="ArgumentException">If operation or message is null or empty</exception>
    public DatabaseError(
        string operation,
        string message,
        string errorCode = UnknownErrorCode,
        ErrorSeverity severity = ErrorSeverity.Error)
        : base(FormatErrorMessage(operation, message, errorCode))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        Operation = operation;
        ErrorCode = errorCode;
        Severity = severity;
        Timestamp = DateTime.UtcNow;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the database operation that failed
    /// </summary>
    public string Operation { get; }

    /// <summary>
    ///     Gets the error code associated with the database error
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    ///     Gets the severity of the database error
    /// </summary>
    public ErrorSeverity Severity { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    ///     Gets whether this is a critical error
    /// </summary>
    public bool IsCritical => Severity == ErrorSeverity.Critical;

    /// <summary>
    ///     Gets whether this is a warning
    /// </summary>
    public bool IsWarning => Severity == ErrorSeverity.Warning;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Creates a new DatabaseError with updated severity
    /// </summary>
    public DatabaseError WithSeverity(ErrorSeverity newSeverity)
    {
        return this with { Severity = newSeverity };
    }

    /// <summary>
    ///     Creates a new DatabaseError with an updated error code
    /// </summary>
    public DatabaseError WithErrorCode(string newErrorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newErrorCode);
        return this with { ErrorCode = newErrorCode, Message = FormatErrorMessage(Operation, Message, newErrorCode) };
    }

    /// <summary>
    ///     Determines if the error matches specific criteria
    /// </summary>
    public bool Matches(string? errorCode = null, ErrorSeverity? severity = null, string? operation = null)
    {
        return (errorCode is null || ErrorCode.Equals(errorCode, StringComparison.OrdinalIgnoreCase)) &&
               (severity is null || Severity == severity) &&
               (operation is null || Operation.Equals(operation, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new critical DatabaseError
    /// </summary>
    public static DatabaseError Critical(string operation, string message, string errorCode = UnknownErrorCode)
    {
        return new DatabaseError(operation, message, errorCode, ErrorSeverity.Critical);
    }

    /// <summary>
    ///     Creates a new warning DatabaseError
    /// </summary>
    public static DatabaseError Warning(string operation, string message, string errorCode = UnknownErrorCode)
    {
        return new DatabaseError(operation, message, errorCode, ErrorSeverity.Warning);
    }

    #endregion
}
