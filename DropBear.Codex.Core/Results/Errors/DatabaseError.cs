#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur during database operations
/// </summary>
public record DatabaseError : ResultError
{
    public enum ErrorSeverity
    {
        Warning,
        Error,
        Critical
    }

    public DatabaseError(string message) : base(message)
    {
        Operation = "Unknown";
        ErrorCode = "UNKNOWN";
        Severity = ErrorSeverity.Error;
    }

    public DatabaseError(
        string operation,
        string message,
        string errorCode = "UNKNOWN",
        ErrorSeverity severity = ErrorSeverity.Error)
        : base(FormatErrorMessage(operation, message, errorCode))
    {
        Operation = operation;
        ErrorCode = errorCode;
        Severity = severity;
    }

    /// <summary>
    ///     Gets the database operation that failed
    /// </summary>
    public string Operation { get; }

    /// <summary>
    ///     Gets the error code associated with the database error
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    ///     Gets the severity of the database error
    /// </summary>
    public ErrorSeverity Severity { get; }

    private static string FormatErrorMessage(string operation, string message, string errorCode)
    {
        return $"Database operation '{operation}' failed with error code {errorCode}: {message}";
    }
}
