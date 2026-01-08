using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using Microsoft.Extensions.Logging;
using Serilog;
using CoreValidationResult = DropBear.Codex.Core.Results.Validations.ValidationResult;
using CoreValidationError = DropBear.Codex.Core.Results.Validations.ValidationError;

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Provides validation helpers optimized for performance and immutability.
/// </summary>
public static partial class ValidationHelper
{
    private static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

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
            LogValidationFailed(Logger, model.GetType().Name, ex);
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
                LogCustomValidationFailed(Logger, model.GetType().Name, ex);
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
            LogValidationWithCustomRulesFailed(Logger, model.GetType().Name, ex);
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

            var results = await Task.WhenAll(validationTasks).ConfigureAwait(false);
            return CoreValidationResult.Combine(results);
        }
        catch (Exception ex)
        {
            LogBatchValidationFailed(Logger, ex);
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

    #region Helper Methods (Logger)

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Core.Logging.LoggerFactory.Logger.ForContext(typeof(ValidationHelper)));
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(ValidationHelper));
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Validation failed for {Type}")]
    static partial void LogValidationFailed(Microsoft.Extensions.Logging.ILogger logger, string type, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Custom validation failed for {Type}")]
    static partial void LogCustomValidationFailed(Microsoft.Extensions.Logging.ILogger logger, string type, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Validation failed for {Type}")]
    static partial void LogValidationWithCustomRulesFailed(Microsoft.Extensions.Logging.ILogger logger, string type, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Batch validation failed")]
    static partial void LogBatchValidationFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    #endregion
}
