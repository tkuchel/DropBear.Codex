#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents a collection of multiple result errors
/// </summary>
public sealed record CompositeError : ResultError
{
    private readonly List<ResultError> _errors;

    #region Private Methods

    private static string FormatErrorMessage(IEnumerable<ResultError> errors)
    {
        var errorList = errors.Where(e => e is not null).ToList();

        if (errorList.Count == 0)
        {
            return "No errors";
        }

        if (errorList.Count == 1)
        {
            return errorList[0].Message;
        }

        return string.Join(
            Environment.NewLine,
            errorList.Select((error, index) => $"{index + 1}. {error.Message}"));
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new CompositeError from a collection of errors
    /// </summary>
    /// <exception cref="ArgumentNullException">If errors is null</exception>
    public CompositeError(IEnumerable<ResultError> errors)
        : base(FormatErrorMessage(errors ?? throw new ArgumentNullException(nameof(errors))))
    {
        _errors = errors.Where(e => e is not null).ToList();
    }

    /// <summary>
    ///     Creates a new CompositeError from a params array of errors
    /// </summary>
    /// <exception cref="ArgumentNullException">If errors is null</exception>
    public CompositeError(params ResultError[] errors)
        : this(errors as IEnumerable<ResultError>)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the collection of individual errors
    /// </summary>
    public IReadOnlyList<ResultError> Errors => _errors.AsReadOnly();

    /// <summary>
    ///     Gets the count of errors
    /// </summary>
    public int Count => _errors.Count;

    /// <summary>
    ///     Gets whether this composite contains any errors
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Gets errors of a specific type
    /// </summary>
    /// <typeparam name="TError">The type of errors to retrieve</typeparam>
    /// <returns>An enumerable of errors of the specified type</returns>
    public IEnumerable<TError> GetErrors<TError>() where TError : ResultError
    {
        return _errors.OfType<TError>();
    }

    /// <summary>
    ///     Creates a new CompositeError with an additional error
    /// </summary>
    /// <param name="error">The error to add</param>
    /// <returns>A new CompositeError instance</returns>
    /// <exception cref="ArgumentNullException">If error is null</exception>
    public CompositeError WithError(ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var newErrors = new List<ResultError>(_errors) { error };
        return new CompositeError(newErrors);
    }

    /// <summary>
    ///     Creates a new CompositeError by merging with another CompositeError
    /// </summary>
    /// <param name="other">The CompositeError to merge with</param>
    /// <returns>A new CompositeError instance</returns>
    /// <exception cref="ArgumentNullException">If other is null</exception>
    public CompositeError Merge(CompositeError other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var mergedErrors = new List<ResultError>(_errors.Count + other._errors.Count);
        mergedErrors.AddRange(_errors);
        mergedErrors.AddRange(other._errors);

        return new CompositeError(mergedErrors);
    }

    /// <summary>
    ///     Checks if this composite contains an error of a specific type
    /// </summary>
    public bool ContainsErrorType<TError>() where TError : ResultError
    {
        return _errors.Any(e => e is TError);
    }

    /// <summary>
    ///     Gets the first error of a specific type, if any
    /// </summary>
    public TError? FirstErrorOfType<TError>() where TError : ResultError
    {
        return _errors.OfType<TError>().FirstOrDefault();
    }

    #endregion
}
