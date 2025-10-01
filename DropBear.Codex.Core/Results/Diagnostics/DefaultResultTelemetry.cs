#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Default implementation of result telemetry with enhanced metrics and OpenTelemetry-ready design.
///     Optimized for .NET 9 with minimal overhead.
/// </summary>
public sealed class DefaultResultTelemetry : IResultTelemetry, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("DropBear.Codex.Core.Results", "1.0.0");
    private static readonly Meter Meter = new("DropBear.Codex.Core.Results", "1.0.0");

    // Metrics
    private readonly Counter<long> _resultCreatedCounter;
    private readonly Counter<long> _resultTransformedCounter;
    private readonly Counter<long> _exceptionCounter;
    private readonly Histogram<double> _operationDuration;

    // Statistics (optional, can be disabled for performance)
    private readonly ConcurrentDictionary<string, long> _operationCounts;
    private readonly ConcurrentDictionary<string, long> _errorCounts;
    private readonly bool _collectStatistics;

    /// <summary>
    ///     Initializes a new instance of DefaultResultTelemetry.
    /// </summary>
    /// <param name="collectStatistics">
    ///     Whether to collect in-memory statistics. Disable for high-performance scenarios.
    /// </param>
    public DefaultResultTelemetry(bool collectStatistics = false)
    {
        _collectStatistics = collectStatistics;

        // Initialize metrics
        _resultCreatedCounter = Meter.CreateCounter<long>(
            "results.created",
            description: "Number of results created");

        _resultTransformedCounter = Meter.CreateCounter<long>(
            "results.transformed",
            description: "Number of result transformations");

        _exceptionCounter = Meter.CreateCounter<long>(
            "results.exceptions",
            description: "Number of exceptions in results");

        _operationDuration = Meter.CreateHistogram<double>(
            "results.operation.duration",
            unit: "ms",
            description: "Duration of result operations");

        // Initialize statistics if enabled
        if (_collectStatistics)
        {
            _operationCounts = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            _errorCounts = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        }
        else
        {
            _operationCounts = null!;
            _errorCounts = null!;
        }
    }

    #region IResultTelemetry Implementation

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultCreated(ResultState state, Type resultType, string? caller = null)
    {
        // Record metric
        _resultCreatedCounter.Add(1,
            new KeyValuePair<string, object?>("state", state.ToString()),
            new KeyValuePair<string, object?>("type", resultType.Name),
            new KeyValuePair<string, object?>("caller", caller ?? "Unknown"));

        // Update statistics if enabled
        if (_collectStatistics)
        {
            var key = $"{resultType.Name}:{state}";
            _operationCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        // Create activity for tracing (if active listener exists)
        if (ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity("ResultCreated", ActivityKind.Internal);
            activity?.SetTag("result.state", state.ToString());
            activity?.SetTag("result.type", resultType.Name);
            activity?.SetTag("caller", caller);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultTransformed(
        ResultState originalState,
        ResultState newState,
        Type resultType,
        string? caller = null)
    {
        // Record metric
        _resultTransformedCounter.Add(1,
            new KeyValuePair<string, object?>("original_state", originalState.ToString()),
            new KeyValuePair<string, object?>("new_state", newState.ToString()),
            new KeyValuePair<string, object?>("type", resultType.Name),
            new KeyValuePair<string, object?>("caller", caller ?? "Unknown"));

        // Update statistics if enabled
        if (_collectStatistics)
        {
            var key = $"{resultType.Name}:Transform:{originalState}->{newState}";
            _operationCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        // Create activity for tracing
        if (ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity("ResultTransformed", ActivityKind.Internal);
            activity?.SetTag("result.original_state", originalState.ToString());
            activity?.SetTag("result.new_state", newState.ToString());
            activity?.SetTag("result.type", resultType.Name);
            activity?.SetTag("caller", caller);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackException(
        Exception exception,
        ResultState state,
        Type resultType,
        string? caller = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Record metric
        _exceptionCounter.Add(1,
            new KeyValuePair<string, object?>("exception_type", exception.GetType().Name),
            new KeyValuePair<string, object?>("state", state.ToString()),
            new KeyValuePair<string, object?>("result_type", resultType.Name),
            new KeyValuePair<string, object?>("caller", caller ?? "Unknown"));

        // Update statistics if enabled
        if (_collectStatistics)
        {
            var key = $"{exception.GetType().Name}:{resultType.Name}";
            _errorCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        // Create activity for tracing
        if (ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity("ResultException", ActivityKind.Internal);
            activity?.SetTag("exception.type", exception.GetType().Name);
            activity?.SetTag("exception.message", exception.Message);
            activity?.SetTag("result.state", state.ToString());
            activity?.SetTag("result.type", resultType.Name);
            activity?.SetTag("caller", caller);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

            // Record exception event
            activity?.AddEvent(new ActivityEvent("exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.StackTrace }
                }));
        }
    }

    #endregion

    #region Additional Tracking Methods

    /// <summary>
    ///     Tracks the duration of an operation.
    /// </summary>
    public void TrackOperationDuration(
        TimeSpan duration,
        Type resultType,
        ResultState state,
        string? operationName = null)
    {
        _operationDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("type", resultType.Name),
            new KeyValuePair<string, object?>("state", state.ToString()),
            new KeyValuePair<string, object?>("operation", operationName ?? "Unknown"));
    }

    /// <summary>
    ///     Creates a traced operation scope for measuring duration.
    /// </summary>
    public IDisposable TraceOperation(string operationName, Type resultType)
    {
        return new OperationScope(this, operationName, resultType);
    }

    #endregion

    #region Statistics (if enabled)

    /// <summary>
    ///     Gets operation statistics if collection is enabled.
    /// </summary>
    public IReadOnlyDictionary<string, long>? GetOperationStatistics()
    {
        return _collectStatistics ? _operationCounts : null;
    }

    /// <summary>
    ///     Gets error statistics if collection is enabled.
    /// </summary>
    public IReadOnlyDictionary<string, long>? GetErrorStatistics()
    {
        return _collectStatistics ? _errorCounts : null;
    }

    /// <summary>
    ///     Clears all collected statistics.
    /// </summary>
    public void ClearStatistics()
    {
        if (_collectStatistics)
        {
            _operationCounts.Clear();
            _errorCounts.Clear();
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    ///     Disposes telemetry resources.
    /// </summary>
    public void Dispose()
    {
        // Meter and ActivitySource are static, don't dispose them
        // Just clear statistics if enabled
        if (_collectStatistics)
        {
            _operationCounts?.Clear();
            _errorCounts?.Clear();
        }
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Represents a scoped operation for duration tracking.
    /// </summary>
    private sealed class OperationScope : IDisposable
    {
        private readonly DefaultResultTelemetry _telemetry;
        private readonly string _operationName;
        private readonly Type _resultType;
        private readonly Stopwatch _stopwatch;
        private readonly Activity? _activity;

        public OperationScope(DefaultResultTelemetry telemetry, string operationName, Type resultType)
        {
            _telemetry = telemetry;
            _operationName = operationName;
            _resultType = resultType;
            _stopwatch = Stopwatch.StartNew();
            _activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal);
            _activity?.SetTag("result.type", resultType.Name);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _telemetry.TrackOperationDuration(
                _stopwatch.Elapsed,
                _resultType,
                ResultState.Success,
                _operationName);
            _activity?.Dispose();
        }
    }

    #endregion
}
