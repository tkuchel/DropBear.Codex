namespace DropBear.Codex.Core.Results.Validations;


/// <summary>
///     Exception thrown when validation fails.
///     Optimized for .NET 9 with modern exception patterns.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    public ValidationException()
        : base("Validation failed")
    {
        Errors = [];
    }

    /// <summary>
    ///     Initializes a new ValidationException from a ValidationResult.
    /// </summary>
    public ValidationException(ValidationResult validationResult)
        : base(CreateMessage(validationResult))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        Errors = [.. validationResult.Errors];
    }

    /// <summary>
    ///     Initializes a new ValidationException with a custom message.
    /// </summary>
    public ValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    /// <summary>
    ///     Initializes a new ValidationException with a message and inner exception.
    /// </summary>
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [];
    }

    /// <summary>
    ///     Initializes a new ValidationException with errors.
    /// </summary>
    public ValidationException(string message, IEnumerable<ValidationError> errors)
        : base(message)
    {
        Errors = [.. errors ?? []];
    }

    /// <summary>
    ///     Gets the validation result that caused this exception.
    /// </summary>
    public ValidationResult? ValidationResult { get; }

    /// <summary>
    ///     Gets all validation errors.
    /// </summary>
    public IReadOnlyCollection<ValidationError> Errors { get; }

    /// <summary>
    ///     Creates an error message from the validation result.
    /// </summary>
    private static string CreateMessage(ValidationResult validationResult)
    {
        if (validationResult is null)
        {
            return "Validation failed";
        }

        if (validationResult.ErrorCount == 1)
        {
            return $"Validation failed: {validationResult.ErrorMessage}";
        }

        return $"Validation failed with {validationResult.ErrorCount} error(s): " +
               string.Join("; ", validationResult.Errors.Select(e => e.Message));
    }
}
