#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents a collection of multiple <see cref="ResultError" /> instances combined into one.
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

        // Create a line-separated list of error messages
        return string.Join(
            Environment.NewLine,
            errorList.Select((error, index) => $"{index + 1}. {error.Message}"));
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new <see cref="CompositeError" /> from a collection of errors.
    /// </summary>
    /// <param name="errors">The collection of <see cref="ResultError" /> to combine.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="errors" /> is null.</exception>
    public CompositeError(IEnumerable<ResultError> errors)
        : base(FormatErrorMessage(errors ?? throw new ArgumentNullException(nameof(errors))))
    {
        _errors = errors.Where(e => e is not null).ToList();
    }

    /// <summary>
    ///     Creates a new <see cref="CompositeError" /> from a params array of errors.
    /// </summary>
    public CompositeError(params ResultError[] errors)
        : this(errors as IEnumerable<ResultError>)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the collection of individual errors as a read-only list.
    /// </summary>
    public IReadOnlyList<ResultError> Errors => _errors.AsReadOnly();

    /// <summary>
    ///     Gets the count of errors contained in this composite.
    /// </summary>
    public int Count => _errors.Count;

    /// <summary>
    ///     Indicates whether this composite contains any errors.
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Gets all errors of type <typeparamref name="TError" />.
    /// </summary>
    public IEnumerable<TError> GetErrors<TError>() where TError : ResultError
    {
        return _errors.OfType<TError>();
    }

    /// <summary>
    ///     Creates a new <see cref="CompositeError" /> with an additional error appended.
    /// </summary>
    /// <param name="error">The <see cref="ResultError" /> to add.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="error" /> is null.</exception>
    public CompositeError WithError(ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var newErrors = new List<ResultError>(_errors) { error };
        return new CompositeError(newErrors);
    }

    /// <summary>
    ///     Merges this composite with another <paramref name="other" /> <see cref="CompositeError" />, producing a new
    ///     composite.
    /// </summary>
    /// <param name="other">Another <see cref="CompositeError" /> to merge with.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="other" /> is null.</exception>
    public CompositeError Merge(CompositeError other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var mergedErrors = new List<ResultError>(_errors.Count + other._errors.Count);
        mergedErrors.AddRange(_errors);
        mergedErrors.AddRange(other._errors);

        return new CompositeError(mergedErrors);
    }

    /// <summary>
    ///     Checks if this composite contains an error of the specified <typeparamref name="TError" /> type.
    /// </summary>
    public bool ContainsErrorType<TError>() where TError : ResultError
    {
        return _errors.Exists(e => e is TError);
    }

    /// <summary>
    ///     Gets the first error of the specified <typeparamref name="TError" /> type, or <c>null</c> if none found.
    /// </summary>
    public TError? FirstErrorOfType<TError>() where TError : ResultError
    {
        return _errors.OfType<TError>().FirstOrDefault();
    }

    #endregion
}
