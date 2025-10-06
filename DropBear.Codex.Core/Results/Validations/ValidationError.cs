#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents a validation error with details about the validation failure.
///     Optimized for .NET 9 with modern C# features and better property handling.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record ValidationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of ValidationError with a message.
    /// </summary>
    public ValidationError(string message) : base(message)
    {
        Message = message;
        Severity = ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Gets the property or field that failed validation.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    ///     Gets the validation rule that failed.
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    ///     Gets the attempted value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; init; }

    /// <summary>
    ///     Gets the path to the property (for nested validations).
    ///     Example: "Address.Street.Number"
    /// </summary>
    public string? PropertyPath { get; init; }

    /// <summary>
    ///     Gets whether this is a property validation error.
    /// </summary>
    public bool IsPropertyError => !string.IsNullOrWhiteSpace(PropertyName);

    /// <summary>
    ///     Gets whether this is a rule validation error.
    /// </summary>
    public bool IsRuleError => !string.IsNullOrWhiteSpace(ValidationRule);

    #region Display

    private string DebuggerDisplay
    {
        get
        {
            var parts = new List<string>(4);

            if (PropertyName is not null)
            {
                parts.Add($"Property: {PropertyName}");
            }

            if (ValidationRule is not null)
            {
                parts.Add($"Rule: {ValidationRule}");
            }

            parts.Add($"Message: {Message}");

            if (AttemptedValue is not null)
            {
                parts.Add($"Value: {AttemptedValue}");
            }

            return string.Join(" | ", parts);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a validation error for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that was attempted.</param>
    /// <returns>A new ValidationError instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError ForProperty(
        string propertyName,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ValidationError(message) { PropertyName = propertyName, AttemptedValue = attemptedValue };
    }

    /// <summary>
    ///     Creates a validation error for a specific property with a path.
    ///     Useful for nested object validation.
    /// </summary>
    /// <param name="propertyPath">The full path to the property (e.g., "Address.Street.Number").</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that was attempted.</param>
    /// <returns>A new ValidationError instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError ForPropertyPath(
        string propertyPath,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // Extract the property name from the path (last segment)
        var propertyName = propertyPath.Contains('.')
            ? propertyPath[(propertyPath.LastIndexOf('.') + 1)..]
            : propertyPath;

        return new ValidationError(message)
        {
            PropertyName = propertyName, PropertyPath = propertyPath, AttemptedValue = attemptedValue
        };
    }

    /// <summary>
    ///     Creates a validation error for a specific validation rule.
    /// </summary>
    /// <param name="rule">The validation rule that failed.</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that was attempted.</param>
    /// <returns>A new ValidationError instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError ForRule(
        string rule,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ValidationError(message) { ValidationRule = rule, AttemptedValue = attemptedValue };
    }

    /// <summary>
    ///     Creates a validation error with both property and rule information.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="rule">The validation rule that failed.</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that was attempted.</param>
    /// <returns>A new ValidationError instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError ForPropertyAndRule(
        string propertyName,
        string rule,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ValidationError(message)
        {
            PropertyName = propertyName, ValidationRule = rule, AttemptedValue = attemptedValue
        };
    }

    /// <summary>
    ///     Creates a required field validation error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError Required(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return ForPropertyAndRule(
            propertyName,
            "Required",
            $"{propertyName} is required.");
    }

    /// <summary>
    ///     Creates a range validation error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError OutOfRange<T>(
        string propertyName,
        T attemptedValue,
        T min,
        T max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return ForPropertyAndRule(
            propertyName,
            "Range",
            $"{propertyName} must be between {min} and {max}.",
            attemptedValue);
    }

    /// <summary>
    ///     Creates a string length validation error.
    ///     Uses modern pattern matching for cleaner code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError InvalidLength(
        string propertyName,
        string attemptedValue,
        int? minLength = null,
        int? maxLength = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var message = (minLength, maxLength) switch
        {
            (not null, not null) => $"{propertyName} must be between {minLength} and {maxLength} characters.",
            (not null, null) => $"{propertyName} must be at least {minLength} characters.",
            (null, not null) => $"{propertyName} must be at most {maxLength} characters.",
            _ => $"{propertyName} has an invalid length."
        };

        return ForPropertyAndRule(
            propertyName,
            "Length",
            message,
            attemptedValue);
    }

    /// <summary>
    ///     Creates an invalid format validation error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError InvalidFormat(
        string propertyName,
        string attemptedValue,
        string? expectedFormat = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var message = expectedFormat is not null
            ? $"{propertyName} must be in the format: {expectedFormat}."
            : $"{propertyName} has an invalid format.";

        return ForPropertyAndRule(
            propertyName,
            "Format",
            message,
            attemptedValue);
    }

    /// <summary>
    ///     Creates an invalid email validation error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError InvalidEmail(string propertyName, string attemptedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return ForPropertyAndRule(
            propertyName,
            "Email",
            $"{propertyName} must be a valid email address.",
            attemptedValue);
    }

    /// <summary>
    ///     Creates a custom validation error with a specific rule name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError Custom(
        string propertyName,
        string ruleName,
        string message,
        object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return ForPropertyAndRule(propertyName, ruleName, message, attemptedValue);
    }

    #endregion

    #region Performance-Optimized String Operations

    /// <summary>
    ///     Creates a validation error with span-based property name.
    ///     Reduces allocations when property name is already a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationError ForProperty(
        ReadOnlySpan<char> propertyName,
        ReadOnlySpan<char> message,
        object? attemptedValue = null)
    {
        // Only allocate strings when necessary
        var propNameStr = propertyName.ToString();
        var messageStr = message.ToString();

        return new ValidationError(messageStr) { PropertyName = propNameStr, AttemptedValue = attemptedValue };
    }

    /// <summary>
    ///     Checks if property name matches using span comparison.
    ///     Avoids string allocation for comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PropertyNameEquals(ReadOnlySpan<char> propertyName) =>
        PropertyName.AsSpan().Equals(propertyName, StringComparison.Ordinal);

    /// <summary>
    ///     Gets a formatted error message using DefaultInterpolatedStringHandler.
    ///     Optimized for .NET 9 with reduced allocations.
    /// </summary>
    public string GetFormattedMessage()
    {
        // Use DefaultInterpolatedStringHandler for better performance
        var handler = new DefaultInterpolatedStringHandler(
            20,
            3);

        if (PropertyName is not null)
        {
            handler.AppendLiteral("[");
            handler.AppendFormatted(PropertyName);
            handler.AppendLiteral("] ");
        }

        if (ValidationRule is not null)
        {
            handler.AppendLiteral("(");
            handler.AppendFormatted(ValidationRule);
            handler.AppendLiteral(") ");
        }

        handler.AppendFormatted(Message);

        return handler.ToStringAndClear();
    }

    #endregion
}
