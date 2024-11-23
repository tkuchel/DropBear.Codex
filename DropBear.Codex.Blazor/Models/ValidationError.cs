namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a single validation error.
/// </summary>
public sealed record ValidationError
{
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
    ///     Gets the UTC timestamp when the error was created.
    /// </summary>
    public DateTime Timestamp { get; }
}
