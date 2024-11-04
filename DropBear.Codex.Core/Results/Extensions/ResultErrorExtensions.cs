#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

// Extension methods for working with these error types
public static class ResultErrorExtensions
{
    /// <summary>
    ///     Converts a collection of validation errors into a single ValidationError
    /// </summary>
    public static ValidationError Combine(this IEnumerable<ValidationError> errors)
    {
        var combined = new ValidationError("Multiple validation errors occurred");
        foreach (var error in errors)
        {
            combined.Merge(error);
        }

        return combined;
    }

    /// <summary>
    ///     Creates a CompositeError from a collection of ResultErrors
    /// </summary>
    public static CompositeError ToCompositeError(this IEnumerable<ResultError> errors)
    {
        return new CompositeError(errors);
    }

    /// <summary>
    ///     Extracts all validation errors from a CompositeError
    /// </summary>
    public static ValidationError? CombineValidationErrors(this CompositeError compositeError)
    {
        var validationErrors = compositeError.GetErrors<ValidationError>().ToList();
        return validationErrors.Any() ? validationErrors.Combine() : null;
    }
}
