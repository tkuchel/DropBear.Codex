using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using Serilog;
using CoreValidationResult = DropBear.Codex.Core.Results.Validations.ValidationResult;
using CoreValidationError = DropBear.Codex.Core.Results.Validations.ValidationError;

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Provides validation helpers optimized for performance and immutability.
/// </summary>
public static class ValidationHelper
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ValidationHelper));

    /// <summary>
    ///     Validates a model using data annotations.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="validationContextFactory">Optional factory for custom validation context.</param>
    public static CoreValidationResult ValidateModel(
        object model,
        Func<object, ValidationContext>? validationContextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            var context = validationContextFactory?.Invoke(model) ?? new ValidationContext(model);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            if (Validator.TryValidateObject(model, context, results, validateAllProperties: true))
            {
                return CoreValidationResult.Success;
            }

            var errors = results
                .SelectMany(result => GetValidationErrors(result))
                .ToList();

            return errors.Count == 0
                ? CoreValidationResult.Success
                : CoreValidationResult.Failed(errors);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Validation failed for {Type}", model.GetType().Name);
            return CoreValidationResult.Failed(
                CoreValidationError.ForProperty("Validation", $"Validation failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Validates a model using both data annotations and custom rules.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="customValidation">Optional custom validation rules.</param>
    /// <param name="validationContextFactory">Optional factory for custom validation context.</param>
    public static CoreValidationResult ValidateModelWithCustomRules(
        object model,
        Action<object, ICollection<CoreValidationError>>? customValidation = null,
        Func<object, ValidationContext>? validationContextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            var standardResult = ValidateModel(model, validationContextFactory);
            if (customValidation == null)
            {
                return standardResult;
            }

            var customErrors = new List<CoreValidationError>();
            try
            {
                customValidation(model, customErrors);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Custom validation failed for {Type}", model.GetType().Name);
                return CoreValidationResult.Failed(
                    CoreValidationError.ForProperty("CustomValidation", $"Custom validation failed: {ex.Message}")
                );
            }

            return customErrors.Count == 0
                ? standardResult
                : CoreValidationResult.Combine(new[]
                {
                    standardResult,
                    CoreValidationResult.Failed(customErrors)
                });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Validation failed for {Type}", model.GetType().Name);
            return CoreValidationResult.Failed(
                CoreValidationError.ForProperty("Validation", $"Validation failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Validates multiple models in parallel.
    /// </summary>
    /// <param name="models">The models to validate.</param>
    /// <param name="validationContextFactory">Optional factory for custom validation context.</param>
    public static async Task<CoreValidationResult> ValidateModelsAsync(
        IEnumerable<object> models,
        Func<object, ValidationContext>? validationContextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(models);

        try
        {
            var modelsList = models.ToList();
            if (modelsList.Count == 0)
            {
                return CoreValidationResult.Success;
            }

            var validationTasks = modelsList
                .Select(model => Task.Run(() => ValidateModel(model, validationContextFactory)))
                .ToList();

            var results = await Task.WhenAll(validationTasks);
            return CoreValidationResult.Combine(results);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Batch validation failed");
            return CoreValidationResult.Failed(
                CoreValidationError.ForProperty("BatchValidation", $"Batch validation failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Converts data annotation validation results to validation errors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IEnumerable<CoreValidationError> GetValidationErrors(
        System.ComponentModel.DataAnnotations.ValidationResult result)
    {
        if (string.IsNullOrEmpty(result.ErrorMessage))
        {
            yield break;
        }

        var members = result.MemberNames;
        var enumerable = members.ToList();
        if (!enumerable.Any())
        {
            yield return CoreValidationError.ForProperty("Model", result.ErrorMessage);
            yield break;
        }

        foreach (var member in enumerable)
        {
            if (string.IsNullOrEmpty(member)) continue;
            yield return CoreValidationError.ForProperty(member, result.ErrorMessage);
        }
    }
}
