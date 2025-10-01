using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents a validation error with details about the validation failure.
///     Optimized for .NET 9 with better property handling.
/// </summary>
public sealed record ValidationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationError"/> class
    ///     with a specific error message.
    /// </summary>
    public ValidationError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the property or field that failed validation.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    ///     Gets or sets the validation rule that failed.
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    ///     Gets or sets the attempted value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this is a property validation error.
    /// </summary>
    public bool IsPropertyError => !string.IsNullOrWhiteSpace(PropertyName);

    /// <summary>
    ///     Gets a value indicating whether this is a rule validation error.
    /// </summary>
    public bool IsRuleError => !string.IsNullOrWhiteSpace(ValidationRule);

    /// <summary>
    ///     Creates a new validation error for a specific property.
    /// </summary>
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
    ///     Creates a new validation error for a specific validation rule.
    /// </summary>
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
    ///     Gets a formatted string representation of the validation error.
    /// </summary>
    public override string ToString()
    {
        if (IsPropertyError && IsRuleError)
        {
            return $"Validation failed for property '{PropertyName}' (Rule: {ValidationRule}): {Message}";
        }

        if (IsPropertyError)
        {
            return $"Validation failed for property '{PropertyName}': {Message}";
        }

        if (IsRuleError)
        {
            return $"Validation rule '{ValidationRule}' failed: {Message}";
        }

        return $"Validation failed: {Message}";
    }
}
