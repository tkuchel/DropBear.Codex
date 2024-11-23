#region

using System.ComponentModel.DataAnnotations;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;
using ValidationResult = DropBear.Codex.Blazor.Models.ValidationResult;

#endregion

namespace DropBear.Codex.Blazor.Helpers;

public static class ValidationHelper
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ValidationHelper));

    public static ValidationResult ValidateModel(object model)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            var validationContext = new ValidationContext(model);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
            {
                var errors = validationResults
                    .SelectMany(result => result.MemberNames.DefaultIfEmpty("Model")
                        .Select(member => new ValidationError(
                            member,
                            result.ErrorMessage ?? "Unknown validation error occurred.")))
                    .ToList();

                return ValidationResult.Failure(
                    errors,
                    "Data annotation validation failed");
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating model of type {ModelType}", model.GetType().Name);
            return ValidationResult.Failure(
                "Validation",
                "An unexpected error occurred during validation",
                ex);
        }
    }

    public static ValidationResult ValidateModelWithCustomRules(
        object model,
        Action<object, List<ValidationError>>? customValidation = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            // First perform standard validation
            var result = ValidateModel(model);

            // If there's no custom validation, return the standard validation result
            if (customValidation == null)
            {
                return result;
            }

            // Perform custom validation
            var customErrors = new List<ValidationError>();
            try
            {
                customValidation(model, customErrors);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during custom validation for {ModelType}", model.GetType().Name);
                return ValidationResult.Failure(
                    "CustomValidation",
                    $"An error occurred during custom validation: {ex.Message}",
                    ex);
            }

            // Combine standard and custom validation results
            return ValidationResult.Combine(
                result,
                customErrors.Any()
                    ? ValidationResult.Failure(customErrors, "Custom validation failed")
                    : ValidationResult.Success());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error validating model of type {ModelType}", model.GetType().Name);
            return ValidationResult.Failure(
                "Validation",
                "An unexpected error occurred during validation",
                ex);
        }
    }
}
