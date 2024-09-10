namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a single validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationError" /> class.
    /// </summary>
    /// <param name="parameter">The name of the parameter that caused the validation error.</param>
    /// <param name="errorMessage">The error message associated with the validation error.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null or empty.</exception>
    public ValidationError(string parameter, string errorMessage)
    {
        Parameter = !string.IsNullOrWhiteSpace(parameter)
            ? parameter
            : throw new ArgumentNullException(nameof(parameter), "Parameter cannot be null or empty.");

        ErrorMessage = !string.IsNullOrWhiteSpace(errorMessage)
            ? errorMessage
            : throw new ArgumentNullException(nameof(errorMessage), "Error message cannot be null or empty.");
    }

    /// <summary>
    ///     Gets the name of the parameter that caused the validation error.
    /// </summary>
    public string Parameter { get; init; }

    /// <summary>
    ///     Gets the error message associated with the validation error.
    /// </summary>
    public string ErrorMessage { get; init; }
}
