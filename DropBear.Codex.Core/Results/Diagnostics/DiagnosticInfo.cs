#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Contains diagnostic information about a result.
/// </summary>
public readonly struct DiagnosticInfo
{
    public ResultState State { get; init; }
    public Type ResultType { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? TraceId { get; init; }

    public DiagnosticInfo(
        ResultState state,
        Type resultType,
        DateTime createdAt,
        string? traceId)
    {
        State = state;
        ResultType = resultType;
        CreatedAt = createdAt;
        TraceId = traceId;
    }
}
