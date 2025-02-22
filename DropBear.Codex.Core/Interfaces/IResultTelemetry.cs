#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides telemetry information for result operations.
/// </summary>
public interface IResultTelemetry
{
    void TrackResultCreated(ResultState state, Type resultType, [CallerMemberName] string? caller = null);

    void TrackResultTransformed(ResultState originalState, ResultState newState, Type resultType,
        [CallerMemberName] string? caller = null);

    void TrackException(Exception exception, ResultState state, Type resultType,
        [CallerMemberName] string? caller = null);
}
