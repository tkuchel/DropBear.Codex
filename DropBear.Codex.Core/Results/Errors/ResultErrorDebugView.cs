#region

using System.Diagnostics;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Provides debug visualization support for ResultError.
/// </summary>
internal sealed class ResultErrorDebugView
{
    private readonly ResultError _error;

    public ResultErrorDebugView(ResultError error)
    {
        _error = error;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public Dictionary<string, object> Items
    {
        get
        {
            var items = new Dictionary<string, object>
                (StringComparer.Ordinal)
                {
                    { "Type", _error.GetType().Name },
                    { "Message", _error.Message },
                    { "Timestamp", _error.Timestamp },
                    { "Age", _error.Age },
                    { "ErrorId", _error.ErrorId }
                };

            if (_error.Metadata != null)
            {
                foreach (var kvp in _error.Metadata)
                {
                    items[$"Metadata.{kvp.Key}"] = kvp.Value;
                }
            }

            return items;
        }
    }
}
