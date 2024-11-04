#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents a collection of multiple errors
/// </summary>
public record CompositeError : ResultError
{
    private readonly List<ResultError> _errors;

    public CompositeError(IEnumerable<ResultError> errors)
        : base(FormatErrorMessage(errors))
    {
        _errors = errors.ToList();
    }

    public CompositeError(params ResultError[] errors)
        : this((IEnumerable<ResultError>)errors)
    {
    }

    /// <summary>
    ///     Gets the collection of individual errors
    /// </summary>
    public IReadOnlyList<ResultError> Errors => _errors.AsReadOnly();

    /// <summary>
    ///     Gets errors of a specific type
    /// </summary>
    public IEnumerable<TError> GetErrors<TError>() where TError : ResultError
    {
        return _errors.OfType<TError>();
    }

    /// <summary>
    ///     Adds an error to the collection
    /// </summary>
    public CompositeError AddError(ResultError error)
    {
        _errors.Add(error);
        return this with { Message = FormatErrorMessage(_errors) };
    }

    /// <summary>
    ///     Merges another composite error into this one
    /// </summary>
    public CompositeError Merge(CompositeError other)
    {
        _errors.AddRange(other._errors);
        return this with { Message = FormatErrorMessage(_errors) };
    }

    private static string FormatErrorMessage(IEnumerable<ResultError> errors)
    {
        var errorMessages = errors
            .Select((error, index) => $"({index + 1}) {error.Message}");
        return string.Join(" | ", errorMessages);
    }
}
