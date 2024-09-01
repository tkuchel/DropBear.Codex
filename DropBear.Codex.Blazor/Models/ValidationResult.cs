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
}
