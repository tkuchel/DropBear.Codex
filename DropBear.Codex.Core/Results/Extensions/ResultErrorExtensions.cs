#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for working with <see cref="ResultError" /> types,
///     including <see cref="ValidationError" /> and <see cref="CompositeError" />.
/// </summary>
public static class ResultErrorExtensions
{
    #region Validation Error Extensions

    /// <summary>
    ///     Combines multiple <see cref="ValidationError" /> instances into a single one by merging their field errors.
    /// </summary>
    /// <param name="errors">The collection of <see cref="ValidationError" /> objects to combine.</param>
    /// <returns>A new <see cref="ValidationError" /> that contains all merged field errors.</returns>
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

        var combined = new ValidationError($"Multiple validation errors occurred ({errorsList.Count})");
        foreach (var error in errorsList)
        {
            combined = combined.Merge(error);
        }

        return combined;
    }

    /// <summary>
    ///     Combines multiple <see cref="ValidationError" /> instances into a single one,
    ///     using a custom <paramref name="message" /> for the resulting <see cref="ValidationError" />.
    /// </summary>
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
    ///     Converts a sequence of <see cref="ResultError" /> objects into a single <see cref="CompositeError" />.
    /// </summary>
    public static CompositeError ToCompositeError(this IEnumerable<ResultError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CompositeError(errors.Where(e => e is not null));
    }

    /// <summary>
    ///     Extracts and combines all <see cref="ValidationError" /> objects from a <see cref="CompositeError" />,
    ///     returning a single <see cref="ValidationError" /> if found, or <c>null</c> if none exist.
    /// </summary>
    public static ValidationError? ExtractValidationErrors(this CompositeError compositeError)
    {
        ArgumentNullException.ThrowIfNull(compositeError);

        var validationErrors = compositeError.GetErrors<ValidationError>().ToList();
        return validationErrors.Count > 0
            ? validationErrors.Combine()
            : null;
    }

    /// <summary>
    ///     Extracts all errors of a specific type <typeparamref name="TError" /> from a <see cref="CompositeError" />.
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
    ///     Checks if a <see cref="ResultError" /> is of type <typeparamref name="TError" />.
    /// </summary>
    public static bool IsErrorType<TError>(this ResultError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is TError;
    }

    /// <summary>
    ///     Attempts to cast a <see cref="ResultError" /> to <typeparamref name="TError" />.
    ///     Returns the casted error if successful, or <c>null</c> if the type is incompatible.
    /// </summary>
    public static TError? AsErrorType<TError>(this ResultError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        return error as TError;
    }

    /// <summary>
    ///     Creates a new <see cref="CompositeError" /> that contains this <paramref name="error" />
    ///     and any additional errors provided.
    /// </summary>
    /// <param name="error">The primary <see cref="ResultError" />.</param>
    /// <param name="additional">An array of additional errors to include.</param>
    /// <returns>A new <see cref="CompositeError" /> containing all errors.</returns>
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
