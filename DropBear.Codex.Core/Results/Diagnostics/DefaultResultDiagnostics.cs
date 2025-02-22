#region

using System.Diagnostics;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Default implementation of result diagnostics.
/// </summary>
public sealed class DefaultResultDiagnostics : IResultDiagnostics
{
    private readonly ActivityContext _activityContext;
    private readonly DateTime _createdAt;
    private readonly Type _resultType;
    private readonly ResultState _state;

    public DefaultResultDiagnostics(ResultState state, Type resultType)
    {
        _state = state;
        _resultType = resultType;
        _createdAt = DateTime.UtcNow;
        _activityContext = Activity.Current?.Context ?? default;
    }

    public DiagnosticInfo GetDiagnostics()
    {
        return new(_state, _resultType, _createdAt, Activity.Current?.Id);
    }

    public ActivityContext GetTraceContext()
    {
        return _activityContext;
    }
}
