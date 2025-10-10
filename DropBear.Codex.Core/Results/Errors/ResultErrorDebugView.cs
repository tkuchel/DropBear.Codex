#region

using System.Diagnostics;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Provides debug visualization support for ResultError.
///     Optimized for .NET 9 with enhanced diagnostics.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
internal sealed class ResultErrorDebugView
{
    private readonly ResultError _error;

    /// <summary>
    ///     Initializes a new instance of the ResultErrorDebugView class.
    /// </summary>
    /// <param name="error">The ResultError to visualize.</param>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
    public ResultErrorDebugView(ResultError error)
    {
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    ///     Gets the error ID.
    /// </summary>
    public string ErrorId => _error.ErrorId;

    /// <summary>
    ///     Gets the age of the error.
    /// </summary>
    public TimeSpan Age => _error.Age;

    /// <summary>
    ///     Gets the formatted age string.
    /// </summary>
    public string AgeFormatted => $"{Age.TotalSeconds:F2}s";

    /// <summary>
    ///     Gets the debug properties for display in the debugger.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public DebugProperty[] Items
    {
        get
        {
            var items = new List<DebugProperty>
            {
                new("Type", _error.GetType().Name),
                new("Message", _error.Message),
                new("Timestamp", _error.Timestamp.ToString("O")),
                new("Age", FormatAge(_error.Age)),
                new("ErrorId", _error.ErrorId),
                new("Severity", _error.Severity.ToString()),
                new("Category", _error.Category.ToString()),
                new("Code", _error.Code ?? "None")
            };

            // Add metadata if present
            if (_error.HasMetadata)
            {
                items.Add(new DebugProperty("MetadataCount", _error.Metadata.Count));

                foreach (var (key, value) in _error.Metadata)
                {
                    items.Add(new DebugProperty($"Metadata.{key}", value));
                }
            }

            return items.ToArray();
        }
    }

    /// <summary>
    ///     Gets the debugger display string.
    /// </summary>
    /// <returns>A formatted string for debugger display.</returns>
    /// <remarks>
    ///     This method is used by the DebuggerDisplay attribute and should not be called directly.
    ///     It safely handles null or missing properties to prevent debugger exceptions.
    /// </remarks>
    private string GetDebuggerDisplay()
    {
        var typeName = _error?.GetType().Name ?? "Unknown";
        var message = _error?.Message ?? "No message";
        return $"{typeName}: {message}";
    }

    /// <summary>
    ///     Formats a TimeSpan into a human-readable age string.
    /// </summary>
    /// <param name="age">The TimeSpan to format.</param>
    /// <returns>A formatted string representing the age (e.g., "500ms", "2.5s", "3.2m").</returns>
    private static string FormatAge(TimeSpan age)
    {
        return age switch
        {
            { TotalMilliseconds: < 1000 } => $"{age.TotalMilliseconds:F0}ms",
            { TotalSeconds: < 60 } => $"{age.TotalSeconds:F1}s",
            { TotalMinutes: < 60 } => $"{age.TotalMinutes:F1}m",
            { TotalHours: < 24 } => $"{age.TotalHours:F1}h",
            _ => $"{age.TotalDays:F1}d"
        };
    }

    /// <summary>
    ///     Represents a property in the debug view.
    /// </summary>
    [DebuggerDisplay("{Value}", Name = "{Key}", Type = "{TypeName}")]
    internal sealed class DebugProperty
    {
        /// <summary>
        ///     Initializes a new instance of the DebugProperty class.
        /// </summary>
        /// <param name="key">The property key/name.</param>
        /// <param name="value">The property value.</param>
        public DebugProperty(string key, object? value)
        {
            Key = key;
            Value = value?.ToString() ?? "(null)";
            TypeName = value?.GetType().Name ?? "null";
        }

        /// <summary>
        ///     Gets the property key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        ///     Gets the property value as a string.
        /// </summary>
        public string Value { get; }

        /// <summary>
        ///     Gets the type name of the property value.
        /// </summary>
        public string TypeName { get; }
    }
}
