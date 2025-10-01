#region

using System.Diagnostics;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Provides debug visualization support for ResultError.
///     Optimized for .NET 9 with enhanced diagnostics.
/// </summary>
[DebuggerDisplay("{_error.GetType().Name}: {_error.Message}")]
internal sealed class ResultErrorDebugView
{
    private readonly ResultError _error;

    public ResultErrorDebugView(ResultError error)
    {
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

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
                new("ErrorId", _error.ErrorId ?? "None"),
                new("Severity", _error.Severity.ToString()),
                new("IsDefaultError", _error.IsDefaultError)
            };

            // Add metadata if present
            if (_error.Metadata != null && _error.Metadata.Count > 0)
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

    [DebuggerDisplay("{Value}", Name = "{Key}", Type = "{TypeName}")]
    internal sealed class DebugProperty
    {
        public DebugProperty(string key, object? value)
        {
            Key = key;
            Value = value?.ToString() ?? "(null)";
            TypeName = value?.GetType().Name ?? "null";
        }

        public string Key { get; }
        public string Value { get; }
        public string TypeName { get; }
    }

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
}
