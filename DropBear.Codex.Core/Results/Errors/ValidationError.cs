#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents validation errors that occur during data validation
/// </summary>
public record ValidationError : ResultError
{
    private readonly Dictionary<string, List<string>> _fieldErrors;

    public ValidationError(string message) : base(message)
    {
        _fieldErrors = new Dictionary<string, List<string>>();
    }

    public ValidationError(string field, string message)
        : this($"{field}: {message}")
    {
        AddFieldError(field, message);
    }

    public ValidationError(IDictionary<string, List<string>> fieldErrors)
        : base(FormatErrorMessage(fieldErrors))
    {
        _fieldErrors = new Dictionary<string, List<string>>(fieldErrors);
    }

    /// <summary>
    ///     Gets a read-only dictionary of field-specific validation errors
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors =>
        _fieldErrors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly());

    /// <summary>
    ///     Adds a validation error for a specific field
    /// </summary>
    public ValidationError AddFieldError(string field, string message)
    {
        if (!_fieldErrors.ContainsKey(field))
        {
            _fieldErrors[field] = new List<string>();
        }

        _fieldErrors[field].Add(message);
        return this;
    }

    /// <summary>
    ///     Merges another validation error into this one
    /// </summary>
    public ValidationError Merge(ValidationError other)
    {
        foreach (var kvp in other._fieldErrors)
        {
            foreach (var message in kvp.Value)
            {
                AddFieldError(kvp.Key, message);
            }
        }

        return this;
    }

    /// <summary>
    ///     Checks if there are any errors for a specific field
    /// </summary>
    public bool HasFieldErrors(string field)
    {
        return _fieldErrors.ContainsKey(field) && _fieldErrors[field].Any();
    }

    /// <summary>
    ///     Gets all error messages for a specific field
    /// </summary>
    public IReadOnlyList<string> GetFieldErrors(string field)
    {
        return _fieldErrors.TryGetValue(field, out var errors)
            ? errors.AsReadOnly()
            : new ReadOnlyCollection<string>(new List<string>());
    }

    private static string FormatErrorMessage(IDictionary<string, List<string>> fieldErrors)
    {
        return string.Join("; ", fieldErrors.Select(kvp =>
            $"{kvp.Key}: {string.Join(", ", kvp.Value)}"));
    }
}
