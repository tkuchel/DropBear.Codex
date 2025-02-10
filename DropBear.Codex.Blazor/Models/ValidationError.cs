namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an immutable validation error with parameter and message.
/// </summary>
public sealed record ValidationError
{
    /// <summary>
    ///     Creates a new validation error.
    /// </summary>
    /// <param name="parameter">The parameter or field that failed validation.</param>
    /// <param name="errorMessage">The error message describing the validation failure.</param>
    /// <exception cref="ArgumentException">If parameter or message is null/empty.</exception>
    public ValidationError(string parameter, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Parameter = parameter;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    ///     Gets the parameter or field that failed validation.
    /// </summary>
    public string Parameter { get; }

    /// <summary>
    ///     Gets the error message describing the validation failure.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///     Gets the timestamp when the validation error occurred.
    /// </summary>
    /// <returns>
    ///    The timestamp when the validation error occurred.
    /// </returns>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public override string ToString() => $"{Parameter}: {ErrorMessage}";
}
