#region

using System.Diagnostics;
using System.Runtime.Serialization;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Base exception for all Result-related errors.
///     Provides enhanced diagnostics and context for .NET 9.
/// </summary>
[DebuggerDisplay("ResultException: {Message}")]
[Serializable]
public class ResultException : Exception
{
    /// <summary>
    ///     Initializes a new instance of ResultException.
    /// </summary>
    public ResultException()
        : base("A result operation failed")
    {
        ResultState = ResultState.Failure;
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public ResultException(string message)
        : base(message)
    {
        ResultState = ResultState.Failure;
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public ResultException(string message, Exception inner)
        : base(message, inner)
    {
        ResultState = ResultState.Failure;
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

    /// <summary>
    ///     Initializes a new instance with serialization info.
    /// </summary>
#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization. Use another constructor.", DiagnosticId = "SYSLIB0051")]
#endif
    protected ResultException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ResultState = (ResultState)(info.GetInt32(nameof(ResultState)));
        Timestamp = info.GetDateTime(nameof(Timestamp));
        ActivityId = info.GetString(nameof(ActivityId));
        OperationName = info.GetString(nameof(OperationName));
    }

    /// <summary>
    ///     Gets the result state associated with this exception.
    /// </summary>
    public ResultState ResultState { get; init; }

    /// <summary>
    ///     Gets the timestamp when this exception was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets the Activity ID for distributed tracing.
    /// </summary>
    public string? ActivityId { get; init; }

    /// <summary>
    ///     Gets or sets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    ///     Gets additional context data for this exception.
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }

    /// <summary>
    ///     Gets the severity of this exception.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.High;

    /// <summary>
    ///     Creates a ResultException with additional context.
    /// </summary>
    public static ResultException WithContext(
        string message,
        string operationName,
        Dictionary<string, object>? context = null,
        Exception? innerException = null)
    {
        return new ResultException(message, innerException!)
        {
            OperationName = operationName,
            Context = context
        };
    }

    /// <summary>
    ///     Adds context to this exception.
    /// </summary>
    public ResultException AddContext(string key, object value)
    {
        var context = Context ?? new Dictionary<string, object>(StringComparer.Ordinal);
        context[key] = value;

        return new ResultException(Message, InnerException!)
        {
            ResultState = ResultState,
            Timestamp = Timestamp,
            ActivityId = ActivityId,
            OperationName = OperationName,
            Context = context,
            Severity = Severity
        };
    }

    /// <summary>
    ///     Gets a detailed string representation for logging.
    /// </summary>
    public string ToDetailedString()
    {
        var details = new System.Text.StringBuilder();
        details.AppendLine($"ResultException: {Message}");
        details.AppendLine($"State: {ResultState}");
        details.AppendLine($"Severity: {Severity}");
        details.AppendLine($"Timestamp: {Timestamp:O}");

        if (!string.IsNullOrEmpty(OperationName))
        {
            details.AppendLine($"Operation: {OperationName}");
        }

        if (!string.IsNullOrEmpty(ActivityId))
        {
            details.AppendLine($"ActivityId: {ActivityId}");
        }

        if (Context != null && Context.Count > 0)
        {
            details.AppendLine("Context:");
            foreach (var (key, value) in Context)
            {
                details.AppendLine($"  {key}: {value}");
            }
        }

        if (InnerException != null)
        {
            details.AppendLine($"Inner: {InnerException.Message}");
        }

        return details.ToString();
    }

    /// <summary>
    ///     Gets serialization data.
    /// </summary>
#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);

        base.GetObjectData(info, context);
        info.AddValue(nameof(ResultState), (int)ResultState);
        info.AddValue(nameof(Timestamp), Timestamp);
        info.AddValue(nameof(ActivityId), ActivityId);
        info.AddValue(nameof(OperationName), OperationName);
    }
}
