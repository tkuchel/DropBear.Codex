#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Represents a validation error with details about the validation failure.
/// </summary>
public sealed record ValidationError : ResultError
{
    public ValidationError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the property or field that failed validation.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    ///     Gets or sets the validation rule that failed.
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    ///     Gets or sets the attempted value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; init; }
}
