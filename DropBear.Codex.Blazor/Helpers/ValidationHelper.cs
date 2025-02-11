using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;
using ValidationResult = DropBear.Codex.Blazor.Models.ValidationResult;

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
    public static ValidationResult ValidateModel(
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
                return ValidationResult.Success();
            }

            var errors = results
                .SelectMany(result => GetValidationErrors(result))
                .ToImmutableArray();

            return errors.IsEmpty
                ? ValidationResult.Success()
                : ValidationResult.Failure(
                    errors,
                    "Data annotation validation failed"
                );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Validation failed for {Type}", model.GetType().Name);
            return ValidationResult.Failure(
                "Validation",
                $"Validation failed: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    ///     Validates a model using both data annotations and custom rules.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="customValidation">Optional custom validation rules.</param>
    /// <param name="validationContextFactory">Optional factory for custom validation context.</param>
    public static ValidationResult ValidateModelWithCustomRules(
        object model,
        Action<object, ICollection<ValidationError>>? customValidation = null,
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

            var customErrors = new List<ValidationError>();
            try
            {
                customValidation(model, customErrors);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Custom validation failed for {Type}", model.GetType().Name);
                return ValidationResult.Failure(
                    "CustomValidation",
                    $"Custom validation failed: {ex.Message}",
                    ex
                );
            }

            return customErrors.Count == 0
                ? standardResult
                : ValidationResult.Combine(new[]
                {
                    standardResult,
                    ValidationResult.Failure(
                        customErrors.ToImmutableArray(),
                        "Custom validation failed"
                    )
                });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Validation failed for {Type}", model.GetType().Name);
            return ValidationResult.Failure(
                "Validation",
                $"Validation failed: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    ///     Validates multiple models in parallel.
    /// </summary>
    /// <param name="models">The models to validate.</param>
    /// <param name="validationContextFactory">Optional factory for custom validation context.</param>
    public static async Task<ValidationResult> ValidateModelsAsync(
        IEnumerable<object> models,
        Func<object, ValidationContext>? validationContextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(models);

        try
        {
            var modelsList = models.ToList();
            if (modelsList.Count == 0)
            {
                return ValidationResult.Success();
            }

            var validationTasks = modelsList
                .Select(model => Task.Run(() => ValidateModel(model, validationContextFactory)))
                .ToList();

            var results = await Task.WhenAll(validationTasks);
            return ValidationResult.Combine(results);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Batch validation failed");
            return ValidationResult.Failure(
                "BatchValidation",
                $"Batch validation failed: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    ///     Converts data annotation validation results to validation errors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IEnumerable<ValidationError> GetValidationErrors(
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
            yield return new ValidationError("Model", result.ErrorMessage);
            yield break;
        }

        foreach (var member in enumerable)
        {
            if (string.IsNullOrEmpty(member)) continue;
            yield return new ValidationError(member, result.ErrorMessage);
        }
    }
}
