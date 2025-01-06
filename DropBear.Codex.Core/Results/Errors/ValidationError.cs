#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents validation errors that occur during data validation,
///     potentially across multiple fields.
/// </summary>
public sealed record ValidationError : ResultError
{
    private readonly Dictionary<string, HashSet<string>> _fieldErrors;

    #region Private Methods

    private static string FormatErrorMessage(IDictionary<string, IEnumerable<string>> fieldErrors)
    {
        if (fieldErrors.Count == 0)
        {
            return "Validation failed";
        }

        // Build a concise message summarizing field errors
        var errorMessages = fieldErrors
            .Where(kvp => kvp.Value.Any())
            .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}");

        return string.Join("; ", errorMessages);
    }

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets a read-only dictionary mapping field names to sets of error messages.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> FieldErrors =>
        _fieldErrors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value,
            StringComparer.Ordinal);

    /// <summary>
    ///     Gets the total count of all field errors combined.
    /// </summary>
    public int ErrorCount => _fieldErrors.Sum(kvp => kvp.Value.Count);

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new <see cref="ValidationError" /> with a general message.
    /// </summary>
    /// <param name="message">The overall validation error message.</param>
    public ValidationError(string message)
        : base(message)
    {
        _fieldErrors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Creates a new <see cref="ValidationError" /> targeting a specific <paramref name="field" /> with
    ///     <paramref name="message" />.
    /// </summary>
    public ValidationError(string field, string message)
        : this($"{field}: {message}")
    {
        AddError(field, message);
    }

    /// <summary>
    ///     Creates a new <see cref="ValidationError" /> from a dictionary mapping fields to enumerations of messages.
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
    ///     Adds a validation error message for the specified <paramref name="field" />.
    /// </summary>
    /// <param name="field">The field that failed validation.</param>
    /// <param name="message">The error message for that field.</param>
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
    ///     Adds multiple validation error messages for the specified <paramref name="field" />.
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
    ///     Merges another <see cref="ValidationError" /> into this one,
    ///     combining field errors from both.
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
    ///     Checks if the specified <paramref name="field" /> has any errors.
    /// </summary>
    public bool HasFieldErrors(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return _fieldErrors.TryGetValue(field, out var errors) && errors.Count > 0;
    }

    /// <summary>
    ///     Gets all error messages for the specified <paramref name="field" />, if any.
    /// </summary>
    public IReadOnlySet<string> GetFieldErrors(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return _fieldErrors.TryGetValue(field, out var errors)
            ? errors
            : new HashSet<string>(StringComparer.Ordinal);
    }

    #endregion
}
