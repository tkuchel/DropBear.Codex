namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a single validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    ///     Gets or sets the name of the parameter that caused the validation error.
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the error message associated with the validation error.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
