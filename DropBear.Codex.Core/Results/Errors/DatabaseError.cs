#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur during database operations,
///     containing operation info, error code, and severity.
/// </summary>
public sealed record DatabaseError : ResultError
{
    #region Enums

    /// <summary>
    ///     The severity level of a database error (Warning, Error, or Critical).
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        ///     Warning-level issues that don't prevent operation completion.
        /// </summary>
        Warning,

        /// <summary>
        ///     Error-level issues that prevent completion but are recoverable.
        /// </summary>
        Error,

        /// <summary>
        ///     Critical issues requiring immediate attention.
        /// </summary>
        Critical
    }

    #endregion

    /// <summary>
    ///     Default error code when none is provided.
    /// </summary>
    private const string UnknownErrorCode = "UNKNOWN";

    /// <summary>
    ///     Default operation name when none is provided.
    /// </summary>
    private const string UnknownOperation = "Unknown";

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
    ///     Creates a new <see cref="DatabaseError" /> with a simple message,
    ///     using <see cref="UnknownOperation" /> as the default operation.
    /// </summary>
    /// <param name="message">The database error message.</param>
    public DatabaseError(string message)
        : this(UnknownOperation, message)
    {
    }

    /// <summary>
    ///     Creates a new <see cref="DatabaseError" /> with detailed information.
    /// </summary>
    /// <param name="operation">The database operation that failed.</param>
    /// <param name="message">A descriptive error message.</param>
    /// <param name="errorCode">An error code to further categorize the issue.</param>
    /// <param name="severity">The <see cref="ErrorSeverity" /> (default is <see cref="ErrorSeverity.Error" />).</param>
    /// <exception cref="ArgumentException">Thrown if required parameters are null or empty.</exception>
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
    ///     Gets the database operation that failed.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    ///     Gets the error code associated with the database error.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    ///     Gets the severity of this database error (Warning, Error, or Critical).
    /// </summary>
    public ErrorSeverity Severity { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when this error was instantiated.
    /// </summary>
    public new DateTime Timestamp { get; }

    /// <summary>
    ///     Indicates whether this is a critical error.
    /// </summary>
    public bool IsCritical => Severity == ErrorSeverity.Critical;

    /// <summary>
    ///     Indicates whether this is a warning-level error.
    /// </summary>
    public bool IsWarning => Severity == ErrorSeverity.Warning;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Returns a new <see cref="DatabaseError" /> with updated severity.
    /// </summary>
    public DatabaseError WithSeverity(ErrorSeverity newSeverity)
    {
        return this with { Severity = newSeverity };
    }

    /// <summary>
    ///     Returns a new <see cref="DatabaseError" /> with an updated error code,
    ///     also updating the recorded message.
    /// </summary>
    public DatabaseError WithErrorCode(string newErrorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newErrorCode);
        return this with { ErrorCode = newErrorCode, Message = FormatErrorMessage(Operation, Message, newErrorCode) };
    }

    /// <summary>
    ///     Checks if this database error matches the specified criteria (error code, severity, operation).
    ///     All specified parameters are matched with case-insensitive comparison.
    /// </summary>
    /// <param name="errorCode">Optional error code to match.</param>
    /// <param name="severity">Optional <see cref="ErrorSeverity" /> to match.</param>
    /// <param name="operation">Optional operation name to match.</param>
    public bool Matches(string? errorCode = null, ErrorSeverity? severity = null, string? operation = null)
    {
        return (errorCode is null || ErrorCode.Equals(errorCode, StringComparison.OrdinalIgnoreCase))
               && (severity is null || Severity == severity)
               && (operation is null || Operation.Equals(operation, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new <see cref="DatabaseError" /> in the Critical severity.
    /// </summary>
    public static DatabaseError Critical(string operation, string message, string errorCode = UnknownErrorCode)
    {
        return new DatabaseError(operation, message, errorCode, ErrorSeverity.Critical);
    }

    /// <summary>
    ///     Creates a new <see cref="DatabaseError" /> in the Warning severity.
    /// </summary>
    public static DatabaseError Warning(string operation, string message, string errorCode = UnknownErrorCode)
    {
        return new DatabaseError(operation, message, errorCode, ErrorSeverity.Warning);
    }

    #endregion
}
