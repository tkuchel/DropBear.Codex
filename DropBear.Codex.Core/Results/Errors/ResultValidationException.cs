#region

using System.Diagnostics;
using System.Runtime.Serialization;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Exception thrown during result validation operations.
///     Provides specific context about validation failures.
/// </summary>
[DebuggerDisplay("ResultValidationException: {Message}")]
[Serializable]
public sealed class ResultValidationException : ResultException
{
    /// <summary>
    ///     Initializes a new instance of ResultValidationException.
    /// </summary>
    public ResultValidationException()
        : base("Result validation failed")
    {
        ResultState = ResultState.Failure;
        Severity = ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public ResultValidationException(string message)
        : base(message)
    {
        ResultState = ResultState.Failure;
        Severity = ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public ResultValidationException(string message, Exception inner)
        : base(message, inner)
    {
        ResultState = ResultState.Failure;
        Severity = ErrorSeverity.Medium;
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    private ResultValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ValidationRule = info.GetString(nameof(ValidationRule));
        PropertyName = info.GetString(nameof(PropertyName));
        AttemptedValue = info.GetValue(nameof(AttemptedValue), typeof(object));
    }

    /// <summary>
    ///     Gets the validation rule that failed.
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    ///     Gets the property name that failed validation.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    ///     Gets the value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; init; }

    /// <summary>
    ///     Creates a validation exception for a specific rule.
    /// </summary>
    public static ResultValidationException ForRule(
        string ruleName,
        string message,
        object? attemptedValue = null)
    {
        return new ResultValidationException(message)
        {
            ValidationRule = ruleName,
            AttemptedValue = attemptedValue,
            OperationName = $"ValidateRule:{ruleName}"
        };
    }

    /// <summary>
    ///     Creates a validation exception for a specific property.
    /// </summary>
    public static ResultValidationException ForProperty(
        string propertyName,
        string message,
        object? attemptedValue = null)
    {
        return new ResultValidationException(message)
        {
            PropertyName = propertyName,
            AttemptedValue = attemptedValue,
            OperationName = $"ValidateProperty:{propertyName}"
        };
    }

    /// <summary>
    ///     Creates a validation exception for state validation.
    /// </summary>
    public static ResultValidationException ForState(
        ResultState expectedState,
        ResultState actualState)
    {
        return new ResultValidationException(
            $"Invalid state transition: expected {expectedState}, but was {actualState}")
        {
            ValidationRule = "StateValidation",
            Context = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ExpectedState"] = expectedState,
                ["ActualState"] = actualState
            }
        };
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);

        base.GetObjectData(info, context);
        info.AddValue(nameof(ValidationRule), ValidationRule);
        info.AddValue(nameof(PropertyName), PropertyName);
        info.AddValue(nameof(AttemptedValue), AttemptedValue);
    }
}
