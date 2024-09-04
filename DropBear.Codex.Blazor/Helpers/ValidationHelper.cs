#region

using System.ComponentModel.DataAnnotations;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

#endregion

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Provides helper methods for validating objects using both Data Annotations and custom validation rules.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    ///     Validates an object using Data Annotations.
    /// </summary>
    /// <param name="model">The object to validate.</param>
    /// <returns>A <see cref="Result{T}" /> of <see cref="IEnumerable{ValidationError}" /> containing any validation errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the model is null.</exception>
    public static Result<IEnumerable<ValidationError>> ValidateModel(object model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "Model cannot be null.");
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);

        if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
        {
            var errors = validationResults.SelectMany(result =>
                result.MemberNames.Select(memberName => new ValidationError
                {
                    Parameter = memberName,
                    ErrorMessage = result.ErrorMessage ?? "Unknown validation error occurred."
                })
            ).ToList();

            return Result<IEnumerable<ValidationError>>.Failure(
                "Validation failed",
                new Exception("Validation errors occurred")
            );
        }

        return Result<IEnumerable<ValidationError>>.Success(Enumerable.Empty<ValidationError>());
    }

    /// <summary>
    ///     Validates an object using both Data Annotations and custom validation rules.
    /// </summary>
    /// <param name="model">The object to validate.</param>
    /// <param name="customValidation">A delegate that performs custom validation on the model.</param>
    /// <returns>A <see cref="Result{T}" /> of <see cref="IEnumerable{ValidationError}" /> containing any validation errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the model or customValidation is null.</exception>
    public static Result<IEnumerable<ValidationError>> ValidateModelWithCustomRules(object model,
        Action<object, List<ValidationError>> customValidation)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "Model cannot be null.");
        }

        if (customValidation == null)
        {
            throw new ArgumentNullException(nameof(customValidation), "Custom validation delegate cannot be null.");
        }

        var result = ValidateModel(model);
        var errors = new List<ValidationError>();

        if (result.IsSuccess)
        {
            try
            {
                customValidation(model, errors);
            }
            catch (Exception ex)
            {
                // Log the exception here if you have a logging mechanism
                errors.Add(new ValidationError
                {
                    Parameter = "CustomValidation",
                    ErrorMessage = $"An error occurred during custom validation: {ex.Message}"
                });
            }
        }
        else
        {
            errors.AddRange(result.Value);
        }

        return errors.Any()
            ? Result<IEnumerable<ValidationError>>.Failure(
                "Validation failed",
                new Exception("Validation errors occurred")
            )
            : Result<IEnumerable<ValidationError>>.Success(Enumerable.Empty<ValidationError>());
    }

    /// <summary>
    ///     Safely retrieves a property value from an object using reflection.
    /// </summary>
    /// <param name="obj">The object to retrieve the property from.</param>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The value of the property, or null if the property doesn't exist or an error occurs.</returns>
    public static object GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        try
        {
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }
}
