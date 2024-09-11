#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Enums;

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
    /// <param name="validationErrors">A collection of validation errors.</param>
    /// <param name="error">The error message associated with the result, if any.</param>
    /// <param name="exception">The exception associated with the result, if any.</param>
    private ValidationResult(IEnumerable<ValidationError> validationErrors, string? error, Exception? exception)
        : base(validationErrors.ToList(), error, exception,
            validationErrors.Any() ? ResultState.Failure : ResultState.Success)
    {
        // Initialize _errors from the already enumerated collection (cached)
        _errors = Value
            .ToList(); // No need to call ToList() again as 'validationErrors' is already enumerated in base constructor
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
    ///     Creates a successful validation result with no errors.
    /// </summary>
    /// <returns>A successful <see cref="ValidationResult" />.</returns>
    public new static ValidationResult Success()
    {
        return new ValidationResult(Enumerable.Empty<ValidationError>(), null, null);
    }

    /// <summary>
    ///     Creates a failed validation result with the provided errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="error">The error message associated with the result, if any.</param>
    /// <param name="exception">The exception associated with the result, if any.</param>
    /// <returns>A failed <see cref="ValidationResult" />.</returns>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors, string? error = null,
        Exception? exception = null)
    {
        return new ValidationResult(errors, error, exception);
    }

    /// <summary>
    ///     Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="error">The validation error.</param>
    /// <param name="errorMessage">The error message associated with the result, if any.</param>
    /// <param name="exception">The exception associated with the result, if any.</param>
    /// <returns>A failed <see cref="ValidationResult" />.</returns>
    public static ValidationResult Failure(ValidationError error, string? errorMessage = null,
        Exception? exception = null)
    {
        return new ValidationResult(new[] { error }, errorMessage, exception);
    }

    /// <summary>
    ///     Adds a single validation error to the ValidationResult.
    /// </summary>
    /// <param name="parameter">The name of the parameter or property that caused the validation error.</param>
    /// <param name="errorMessage">The error message describing the validation failure.</param>
    public void AddError(string parameter, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentNullException(nameof(parameter), "Parameter cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentNullException(nameof(errorMessage), "Error message cannot be null or empty.");
        }

        _errors.Add(new ValidationError(parameter, errorMessage));
        UpdateState();
    }

    /// <summary>
    ///     Adds multiple validation errors to the ValidationResult.
    /// </summary>
    /// <param name="errors">An IEnumerable of ValidationError objects to be added.</param>
    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        if (errors == null || !errors.Any())
        {
            throw new ArgumentNullException(nameof(errors), "Errors cannot be null or empty.");
        }

        _errors.AddRange(errors);
        UpdateState();
    }

    /// <summary>
    ///     Updates the internal state of the ValidationResult.
    /// </summary>
    private void UpdateState()
    {
        // Set Value to _errors directly, which is already a cached collection
        Value = _errors;
        State = _errors.Any() ? ResultState.Failure : ResultState.Success;
    }
}
