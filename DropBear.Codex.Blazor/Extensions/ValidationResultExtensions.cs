#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Provides extension methods for converting between different ValidationResult implementations.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    ///     Converts a Core ValidationResult to a Blazor ValidationResult.
    /// </summary>
    /// <param name="coreResult">The Core ValidationResult to convert.</param>
    /// <returns>A Blazor ValidationResult with equivalent state and errors.</returns>
    public static ValidationResult ToBlazorValidationResult(
        this Core.Results.Validations.ValidationResult coreResult)
    {
        if (coreResult == null)
        {
            throw new ArgumentNullException(nameof(coreResult));
        }

        if (coreResult.IsValid)
        {
            return ValidationResult.Success();
        }

        var blazorErrors = new List<ValidationError>();

        // Convert core error to Blazor error format
        if (coreResult.Error != null)
        {
            // Handle property-based error
            if (!string.IsNullOrEmpty(coreResult.Error.PropertyName))
            {
                blazorErrors.Add(
                    new ValidationError(
                        coreResult.Error.PropertyName,
                        coreResult.Error.Message));
            }
            // Handle rule-based error
            else if (!string.IsNullOrEmpty(coreResult.Error.ValidationRule))
            {
                blazorErrors.Add(
                    new ValidationError(
                        coreResult.Error.ValidationRule,
                        coreResult.Error.Message));
            }
            // Handle general errors
            else
            {
                blazorErrors.Add(
                    new ValidationError(
                        "General",
                        coreResult.Error.Message));
            }
        }

        return ValidationResult.Failure(
            blazorErrors,
            coreResult.ErrorMessage,
            coreResult.Exception);
    }

    /// <summary>
    ///     Converts a Blazor ValidationResult to a Core ValidationResult.
    /// </summary>
    /// <param name="blazorResult">The Blazor ValidationResult to convert.</param>
    /// <returns>A Core ValidationResult with equivalent state and errors.</returns>
    public static Core.Results.Validations.ValidationResult ToCoreValidationResult(
        this ValidationResult blazorResult)
    {
        if (blazorResult == null)
        {
            throw new ArgumentNullException(nameof(blazorResult));
        }

        if (blazorResult.IsSuccess)
        {
            return Core.Results.Validations.ValidationResult.Success;
        }

        // Handle multiple errors by combining them
        if (blazorResult.Errors.Count > 1)
        {
            var results = blazorResult.Errors.Select(e =>
                Core.Results.Validations.ValidationResult.PropertyFailed(
                    e.Parameter,
                    e.ErrorMessage));

            return Core.Results.Validations.ValidationResult.Combine(results);
        }

        // Handle single error
        if (blazorResult.Errors.Count == 1)
        {
            var error = blazorResult.Errors[0];
            return Core.Results.Validations.ValidationResult.PropertyFailed(
                error.Parameter,
                error.ErrorMessage);
        }

        // If we have a message but no specific errors
        if (!string.IsNullOrEmpty(blazorResult.Message))
        {
            return Core.Results.Validations.ValidationResult.Failed(blazorResult.Message);
        }

        // Fallback
        return Core.Results.Validations.ValidationResult.Failed("Validation failed");
    }
}
