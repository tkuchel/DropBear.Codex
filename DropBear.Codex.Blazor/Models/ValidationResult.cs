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
        : base(validationErrors?.ToList() ?? new List<ValidationError>(), error, exception,
            validationErrors?.Any() == true ? ResultState.Failure : ResultState.Success)
    {
        _errors = Value.ToList();
    }

    /// <summary>
    ///     Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid => !Value.Any();

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
    /// <remarks>
    ///     This method creates a new ValidationError instance and adds it to the internal list of errors.
    ///     It also updates the overall state of the ValidationResult to reflect the addition of the new error.
    /// </remarks>
    public void AddError(string parameter, string errorMessage)
    {
        _errors.Add(new ValidationError { Parameter = parameter, ErrorMessage = errorMessage });
        UpdateState();
    }

    /// <summary>
    ///     Adds multiple validation errors to the ValidationResult.
    /// </summary>
    /// <param name="errors">An IEnumerable of ValidationError objects to be added.</param>
    /// <remarks>
    ///     This method adds all the provided ValidationError instances to the internal list of errors.
    ///     It also updates the overall state of the ValidationResult to reflect the addition of the new errors.
    /// </remarks>
    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        _errors.AddRange(errors);
        UpdateState();
    }

    /// <summary>
    ///     Updates the internal state of the ValidationResult.
    /// </summary>
    private void UpdateState()
    {
        Value = _errors;
        State = _errors.Any() ? ResultState.Failure : ResultState.Success;
    }
}
