#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for working with Result error types
/// </summary>
public static class ResultErrorExtensions
{
    #region Validation Error Extensions

    /// <summary>
    ///     Combines multiple validation errors into a single ValidationError
    /// </summary>
    /// <param name="errors">The collection of validation errors to combine</param>
    /// <returns>A combined ValidationError containing all error messages</returns>
    /// <exception cref="ArgumentNullException">If errors is null</exception>
    public static ValidationError Combine(this IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorsList = errors.Where(e => e is not null).ToList();
        if (errorsList.Count == 0)
        {
            return new ValidationError("No validation errors occurred");
        }

        if (errorsList.Count == 1)
        {
            return errorsList[0];
        }

        var combined = new ValidationError(
            $"Multiple validation errors occurred ({errorsList.Count})");

        foreach (var error in errorsList)
        {
            combined = combined.Merge(error);
        }

        return combined;
    }

    /// <summary>
    ///     Combines multiple validation errors into a single ValidationError with a custom message
    /// </summary>
    /// <param name="errors">The collection of validation errors to combine</param>
    /// <param name="message">Custom message for the combined error</param>
    /// <returns>A combined ValidationError containing all error messages</returns>
    public static ValidationError Combine(this IEnumerable<ValidationError> errors, string message)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var combined = new ValidationError(message);
        foreach (var error in errors.Where(e => e is not null))
        {
            combined = combined.Merge(error);
        }

        return combined;
    }

    #endregion

    #region Composite Error Extensions

    /// <summary>
    ///     Creates a CompositeError from a collection of ResultErrors
    /// </summary>
    /// <param name="errors">The collection of errors to combine</param>
    /// <returns>A new CompositeError containing all errors</returns>
    /// <exception cref="ArgumentNullException">If errors is null</exception>
    public static CompositeError ToCompositeError(this IEnumerable<ResultError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CompositeError(errors.Where(e => e is not null));
    }

    /// <summary>
    ///     Extracts and combines all validation errors from a CompositeError
    /// </summary>
    /// <param name="compositeError">The CompositeError to extract validation errors from</param>
    /// <returns>A combined ValidationError, or null if no validation errors exist</returns>
    public static ValidationError? ExtractValidationErrors(this CompositeError compositeError)
    {
        ArgumentNullException.ThrowIfNull(compositeError);

        var validationErrors = compositeError.GetErrors<ValidationError>().ToList();
        return validationErrors.Count > 0
            ? validationErrors.Combine()
            : null;
    }

    /// <summary>
    ///     Extracts all errors of a specific type from a CompositeError
    /// </summary>
    public static IReadOnlyList<TError> ExtractErrors<TError>(this CompositeError compositeError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(compositeError);
        return compositeError.GetErrors<TError>().ToList().AsReadOnly();
    }

    #endregion

    #region General Error Extensions

    /// <summary>
    ///     Determines if a ResultError is of a specific error type
    /// </summary>
    public static bool IsErrorType<TError>(this ResultError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is TError;
    }

    /// <summary>
    ///     Attempts to cast a ResultError to a specific error type
    /// </summary>
    public static TError? AsErrorType<TError>(this ResultError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        return error as TError;
    }

    /// <summary>
    ///     Creates a new CompositeError by combining multiple ResultErrors
    /// </summary>
    public static CompositeError Combine(this ResultError error, params ResultError[] additional)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(additional);

        var allErrors = new List<ResultError>(1 + additional.Length) { error };
        allErrors.AddRange(additional.Where(e => e is not null));

        return new CompositeError(allErrors);
    }

    #endregion
}
