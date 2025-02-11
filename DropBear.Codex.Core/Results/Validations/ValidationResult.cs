namespace DropBear.Codex.Core.Results.Validations;

#region Validation Classes

/// <summary>
///     Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationResult" /> class.
    /// </summary>
    /// <param name="isValid">Whether the validation was successful.</param>
    /// <param name="errorMessage">The error message in case of failure.</param>
    public ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    ///     Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    ///     Gets the error message if validation failed.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///     Returns a successful validation result.
    /// </summary>
    public static ValidationResult Success { get; } = new(true, null);
}

#endregion
