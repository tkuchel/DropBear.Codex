namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a single validation error, including the parameter that failed
///     and an error message explaining the failure.
/// </summary>
public sealed record ValidationError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationError" /> record.
    /// </summary>
    /// <param name="parameter">The parameter or field that caused the validation error.</param>
    /// <param name="errorMessage">A message describing what went wrong.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="parameter" /> or <paramref name="errorMessage" /> is null or empty.
    /// </exception>
    public ValidationError(string parameter, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Parameter = parameter;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the parameter or field that caused the validation error.
    /// </summary>
    public string Parameter { get; }

    /// <summary>
    ///     Gets the error message describing the validation failure.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///     Gets the UTC timestamp when this error was created.
    /// </summary>
    public DateTime Timestamp { get; }
}
