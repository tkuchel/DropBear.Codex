#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents validation errors that occur during data validation
/// </summary>
public sealed record ValidationError : ResultError
{
    private readonly Dictionary<string, HashSet<string>> _fieldErrors;

    #region Public Properties

    /// <summary>
    ///     Gets a read-only dictionary of field-specific validation errors
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> FieldErrors =>
        _fieldErrors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value,
            StringComparer.Ordinal);

    #endregion

    #region Private Methods

    private static string FormatErrorMessage(IDictionary<string, IEnumerable<string>> fieldErrors)
    {
        if (fieldErrors.Count == 0)
        {
            return "Validation failed";
        }

        var errorMessages = fieldErrors
            .Where(kvp => kvp.Value.Any())
            .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}");

        return string.Join("; ", errorMessages);
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new ValidationError with a general message
    /// </summary>
    public ValidationError(string message) : base(message)
    {
        _fieldErrors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Creates a new ValidationError with a specific field error
    /// </summary>
    public ValidationError(string field, string message)
        : this($"{field}: {message}")
    {
        AddError(field, message);
    }

    /// <summary>
    ///     Creates a new ValidationError from a dictionary of field errors
    /// </summary>
    public ValidationError(IDictionary<string, IEnumerable<string>> fieldErrors)
        : this(FormatErrorMessage(fieldErrors))
    {
        foreach (var (field, messages) in fieldErrors)
        {
            var uniqueMessages = new HashSet<string>(messages, StringComparer.Ordinal);
            if (uniqueMessages.Count > 0)
            {
                _fieldErrors[field] = uniqueMessages;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Adds a validation error for a specific field
    /// </summary>
    public ValidationError AddError(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!_fieldErrors.TryGetValue(field, out var errors))
        {
            errors = new HashSet<string>(StringComparer.Ordinal);
            _fieldErrors[field] = errors;
        }

        errors.Add(message);
        return this;
    }

    /// <summary>
    ///     Adds multiple validation errors for a specific field
    /// </summary>
    public ValidationError AddErrors(string field, IEnumerable<string> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(messages);

        if (!_fieldErrors.TryGetValue(field, out var errors))
        {
            errors = new HashSet<string>(StringComparer.Ordinal);
            _fieldErrors[field] = errors;
        }

        foreach (var message in messages.Where(m => !string.IsNullOrWhiteSpace(m)))
        {
            errors.Add(message);
        }

        return this;
    }

    /// <summary>
    ///     Merges another ValidationError's errors into this one
    /// </summary>
    public ValidationError Merge(ValidationError other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var (field, messages) in other._fieldErrors)
        {
            if (!_fieldErrors.TryGetValue(field, out var existing))
            {
                existing = new HashSet<string>(StringComparer.Ordinal);
                _fieldErrors[field] = existing;
            }

            existing.UnionWith(messages);
        }

        return this;
    }

    /// <summary>
    ///     Checks if there are any errors for a specific field
    /// </summary>
    public bool HasFieldErrors(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return _fieldErrors.TryGetValue(field, out var errors) && errors.Count > 0;
    }

    /// <summary>
    ///     Gets all error messages for a specific field
    /// </summary>
    public IReadOnlySet<string> GetFieldErrors(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return _fieldErrors.TryGetValue(field, out var errors)
            ? errors
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets the total number of field errors
    /// </summary>
    public int ErrorCount => _fieldErrors.Sum(kvp => kvp.Value.Count);

    #endregion
}
