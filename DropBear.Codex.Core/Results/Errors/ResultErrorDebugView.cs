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

    public string ErrorId => _error.ErrorId;
    public TimeSpan Age => _error.Age;
    public string AgeFormatted => $"{Age.TotalSeconds:F2}s";

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
}
