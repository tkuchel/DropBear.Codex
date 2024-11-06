#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the result of a validation operation, inheriting from the base Result class.
/// </summary>
public sealed class ValidationResult : Result<IEnumerable<ValidationError>>
{
    private readonly List<ValidationError> _errors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationResult" /> class.
    /// </summary>
    private ValidationResult(IEnumerable<ValidationError> validationErrors, string? error, Exception? exception,
        ResultState state)
        : base(validationErrors.ToList(), error, exception, state)
    {
        _errors = Value.ToList();
    }

    /// <summary>
    ///     Gets the validation errors in a read-only format.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

    /// <summary>
    ///     Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid => !_errors.Any();

    /// <summary>
    ///     Creates a new ValidationResult instance with the current errors.
    /// </summary>
    private ValidationResult CreateNewResult()
    {
        var state = _errors.Any() ? ResultState.Failure : ResultState.Success;
        return new ValidationResult(_errors, ErrorMessage, Exception, state);
    }

    /// <summary>
    ///     Adds a single validation error to the ValidationResult.
    /// </summary>
    public ValidationResult AddError(string parameter, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentNullException(nameof(parameter), "Parameter cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentNullException(nameof(errorMessage), "Error message cannot be null or empty.");
        }

        var newErrors = new List<ValidationError>(_errors) { new(parameter, errorMessage) };
        return new ValidationResult(newErrors, ErrorMessage, Exception, ResultState.Failure);
    }

    /// <summary>
    ///     Adds multiple validation errors to the ValidationResult.
    /// </summary>
    public ValidationResult AddErrors(IEnumerable<ValidationError> errors)
    {
        if (errors == null || !errors.Any())
        {
            throw new ArgumentNullException(nameof(errors), "Errors cannot be null or empty.");
        }

        var newErrors = new List<ValidationError>(_errors);
        newErrors.AddRange(errors);
        return new ValidationResult(newErrors, ErrorMessage, Exception, ResultState.Failure);
    }

    // Factory methods
    public new static ValidationResult Success()
    {
        return new ValidationResult(Enumerable.Empty<ValidationError>(), null, null, ResultState.Success);
    }

    public static ValidationResult Failure(IEnumerable<ValidationError> errors, string? error = null,
        Exception? exception = null)
    {
        return new ValidationResult(errors, error, exception, ResultState.Failure);
    }

    public static ValidationResult Failure(ValidationError error, string? errorMessage = null,
        Exception? exception = null)
    {
        return new ValidationResult(new[] { error }, errorMessage, exception, ResultState.Failure);
    }
}
