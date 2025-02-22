#region

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Default implementation of result telemetry using OpenTelemetry.
/// </summary>
public sealed class DefaultResultTelemetry : IResultTelemetry
{
    private static readonly ActivitySource ActivitySource = new("DropBear.Codex.Core.Results");
    private static readonly Meter Meter = new("DropBear.Codex.Core.Results");
    private readonly Counter<int> _exceptionCounter;

    private readonly Counter<int> _resultCreatedCounter;
    private readonly Counter<int> _resultTransformedCounter;

    public DefaultResultTelemetry()
    {
        _resultCreatedCounter = Meter.CreateCounter<int>("results.created");
        _resultTransformedCounter = Meter.CreateCounter<int>("results.transformed");
        _exceptionCounter = Meter.CreateCounter<int>("results.exceptions");
    }

    public void TrackResultCreated(ResultState state, Type resultType, string? caller = null)
    {
        using var activity = ActivitySource.StartActivity("ResultCreated");
        activity?.SetTag("resultType", resultType.Name);
        activity?.SetTag("state", state.ToString());
        activity?.SetTag("caller", caller);

        _resultCreatedCounter.Add(1, new KeyValuePair<string, object?>("state", state.ToString()));
    }

    public void TrackResultTransformed(ResultState originalState, ResultState newState, Type resultType,
        string? caller = null)
    {
        using var activity = ActivitySource.StartActivity("ResultTransformed");
        activity?.SetTag("resultType", resultType.Name);
        activity?.SetTag("originalState", originalState.ToString());
        activity?.SetTag("newState", newState.ToString());
        activity?.SetTag("caller", caller);

        _resultTransformedCounter.Add(1);
    }

    public void TrackException(Exception exception, ResultState state, Type resultType, string? caller = null)
    {
        using var activity = ActivitySource.StartActivity("ResultException");
        activity?.SetTag("resultType", resultType.Name);
        activity?.SetTag("state", state.ToString());
        activity?.SetTag("exceptionType", exception.GetType().Name);
        activity?.SetTag("caller", caller);

        _exceptionCounter.Add(1, new KeyValuePair<string, object?>("exceptionType", exception.GetType().Name));
    }
}
