#region

using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Exception thrown when validation fails in a Result context.
///     Optimized for .NET 9 with modern exception patterns.
/// </summary>
public class ResultValidationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of ResultValidationException.
    /// </summary>
    public ResultValidationException()
        : base("Validation failed")
    {
        ValidationResult = ValidationResult.Success;
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public ResultValidationException(string message)
        : base(message)
    {
        ValidationResult = ValidationResult.Success;
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public ResultValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationResult = ValidationResult.Success;
    }

    /// <summary>
    ///     Initializes a new instance with a validation result.
    /// </summary>
    public ResultValidationException(ValidationResult validationResult)
        : base(CreateMessageFromValidationResult(validationResult))
    {
        ValidationResult = validationResult ?? ValidationResult.Success;
    }

    /// <summary>
    ///     Initializes a new instance with a validation result and custom message.
    /// </summary>
    public ResultValidationException(string message, ValidationResult validationResult)
        : base(message)
    {
        ValidationResult = validationResult ?? ValidationResult.Success;
    }

    /// <summary>
    ///     Initializes a new instance with a validation result, message, and inner exception.
    /// </summary>
    public ResultValidationException(
        string message,
        ValidationResult validationResult,
        Exception innerException)
        : base(message, innerException)
    {
        ValidationResult = validationResult ?? ValidationResult.Success;
    }

    /// <summary>
    ///     Gets the validation result associated with this exception.
    /// </summary>
    public ValidationResult ValidationResult { get; }

    /// <summary>
    ///     Gets the validation errors from the validation result.
    /// </summary>
    public IReadOnlyCollection<ValidationError> ValidationErrors => ValidationResult.Errors;

    /// <summary>
    ///     Gets a value indicating whether there are any validation errors.
    /// </summary>
    public bool HasValidationErrors => !ValidationResult.IsValid;

    #region Helper Methods

    /// <summary>
    ///     Creates a message from a validation result.
    /// </summary>
    private static string CreateMessageFromValidationResult(ValidationResult? validationResult)
    {
        if (validationResult is null || validationResult.IsValid)
        {
            return "Validation failed";
        }

        var errors = validationResult.Errors.ToList();
        var errorCount = errors.Count;

        if (errorCount == 1)
        {
            return $"Validation failed: {errors[0].Message}";
        }

        return $"Validation failed with {errorCount} error(s): " +
               string.Join("; ", errors.Select(e => e.Message));
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a ResultValidationException from a validation result.
    /// </summary>
    public static ResultValidationException FromValidationResult(ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        return new ResultValidationException(validationResult);
    }

    /// <summary>
    ///     Creates a ResultValidationException from a single validation error.
    /// </summary>
    public static ResultValidationException FromError(ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ResultValidationException(ValidationResult.Failed(error));
    }

    /// <summary>
    ///     Creates a ResultValidationException from multiple validation errors.
    /// </summary>
    public static ResultValidationException FromErrors(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var errorList = errors.ToList();

        if (errorList.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required", nameof(errors));
        }

        return new ResultValidationException(ValidationResult.Failed(errorList));
    }

    /// <summary>
    ///     Creates a ResultValidationException from multiple validation errors.
    /// </summary>
    public static ResultValidationException FromErrors(params ValidationError[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return FromErrors((IEnumerable<ValidationError>)errors);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///     Gets a formatted summary of all validation errors.
    /// </summary>
    public string GetErrorSummary()
    {
        if (ValidationResult.IsValid)
        {
            return "No validation errors";
        }

        return string.Join(Environment.NewLine,
            ValidationResult.Errors.Select((e, i) => $"{i + 1}. {e.Message}"));
    }

    /// <summary>
    ///     Gets validation errors grouped by property name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> GetErrorsByProperty()
    {
        if (ValidationResult.IsValid)
        {
            return new Dictionary<string, IReadOnlyList<ValidationError>>();
        }

        return ValidationResult.Errors
            .GroupBy(e => e.PropertyName ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ValidationError>)g.ToList(),
                StringComparer.Ordinal);
    }

    #endregion
}
