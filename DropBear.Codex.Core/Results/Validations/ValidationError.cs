using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents a validation error with details about the validation failure.
///     Provides additional context specific to the validation domain.
/// </summary>
public sealed record ValidationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationError"/> class
    ///     with a specific error message.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
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
    ///     Creates a new validation error for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that failed validation.</param>
    /// <returns>A new ValidationError configured for property validation.</returns>
    public static ValidationError ForProperty(string propertyName, string message, object? attemptedValue = null)
    {
        return new ValidationError(message)
        {
            PropertyName = propertyName,
            AttemptedValue = attemptedValue
        };
    }

    /// <summary>
    ///     Creates a new validation error for a specific validation rule.
    /// </summary>
    /// <param name="rule">The name of the validation rule that failed.</param>
    /// <param name="message">The validation error message.</param>
    /// <param name="attemptedValue">The value that failed validation.</param>
    /// <returns>A new ValidationError configured for rule validation.</returns>
    public static ValidationError ForRule(string rule, string message, object? attemptedValue = null)
    {
        return new ValidationError(message)
        {
            ValidationRule = rule,
            AttemptedValue = attemptedValue
        };
    }
}
