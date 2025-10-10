#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Default implementation of result telemetry with enhanced metrics and OpenTelemetry-ready design.
///     Optimized for .NET 9 with configurable modes and minimal overhead.
/// </summary>
public sealed class DefaultResultTelemetry : IResultTelemetry, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("DropBear.Codex.Core.Results", "2.0.0");
    private static readonly Meter Meter = new("DropBear.Codex.Core.Results", "2.0.0");

    // Channel-based processing (for BackgroundChannel mode)
    private readonly Channel<TelemetryEvent>? _channel;
    private readonly CancellationTokenSource? _cts;
    private readonly Counter<long> _exceptionCounter;
    private readonly TelemetryMode _mode;
    private readonly Histogram<double> _operationDuration;

    // Configuration
    private readonly TelemetryOptions _options;
    private readonly Task? _processorTask;

    // Metrics
    private readonly Counter<long> _resultCreatedCounter;
    private readonly Counter<long> _resultTransformedCounter;

    // Statistics (optional)
    private readonly ConcurrentDictionary<string, PoolStatistics>? _statistics;

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of DefaultResultTelemetry.
    /// </summary>
    /// <param name="options">The telemetry options. If null, uses default FireAndForget mode.</param>
    public DefaultResultTelemetry(TelemetryOptions? options = null)
    {
        _options = options ?? new TelemetryOptions();
        _mode = _options.Mode;

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
            "ms",
            "Duration of result operations");

        // Initialize statistics if enabled
        if (_options.CollectStatistics)
        {
            _statistics = new ConcurrentDictionary<string, PoolStatistics>(StringComparer.Ordinal);
        }

        // Initialize channel-based processing if needed
        if (_mode == TelemetryMode.BackgroundChannel)
        {
            var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
            {
                FullMode = _options.FullMode switch
                {
                    ChannelFullMode.Wait => BoundedChannelFullMode.Wait,
                    ChannelFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
                    ChannelFullMode.DropNewest => BoundedChannelFullMode.DropWrite,
                    _ => BoundedChannelFullMode.DropOldest
                },
                SingleReader = true,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<TelemetryEvent>(channelOptions);
            _cts = _options.CancellationTokenSource ?? new CancellationTokenSource();
            _processorTask = Task.Run(() => ProcessTelemetryEventsAsync(_cts.Token), _cts.Token);
        }
    }

    #region IDisposable

    /// <summary>
    ///     Disposes the telemetry instance and stops background processing if active.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal cancellation for background processor
        _cts?.Cancel();

        // Complete the channel to signal no more writes
        _channel?.Writer.Complete();

        // Wait for processor to finish (with timeout)
        if (_processorTask != null)
        {
            try
            {
                _processorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Ignore - already cancelled
            }
        }

        // Dispose resources owned by this instance
        _cts?.Dispose();
    }

    #endregion

    #region IResultTelemetry Implementation

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultCreated(ResultState state, Type resultType, string? caller = null)
    {
        if (_mode == TelemetryMode.Disabled)
        {
            return;
        }

        var eventData = new TelemetryEvent
        {
            Type = TelemetryEventType.ResultCreated,
            State = state,
            ResultType = resultType,
            Caller = caller,
            Timestamp = DateTimeOffset.UtcNow
        };

        ProcessEvent(eventData);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultTransformed(
        ResultState originalState,
        ResultState newState,
        Type resultType,
        string? caller = null)
    {
        if (_mode == TelemetryMode.Disabled)
        {
            return;
        }

        var eventData = new TelemetryEvent
        {
            Type = TelemetryEventType.ResultTransformed,
            State = newState,
            OriginalState = originalState,
            ResultType = resultType,
            Caller = caller,
            Timestamp = DateTimeOffset.UtcNow
        };

        ProcessEvent(eventData);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackException(
        Exception exception,
        ResultState state,
        Type resultType,
        string? caller = null)
    {
        if (_mode == TelemetryMode.Disabled)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(exception);

        var eventData = new TelemetryEvent
        {
            Type = TelemetryEventType.Exception,
            State = state,
            ResultType = resultType,
            Exception = exception,
            Caller = caller,
            Timestamp = DateTimeOffset.UtcNow
        };

        ProcessEvent(eventData);
    }

    #endregion

    #region Event Processing

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessEvent(TelemetryEvent eventData)
    {
        switch (_mode)
        {
            case TelemetryMode.Disabled:
                return;

            case TelemetryMode.Synchronous:
                ProcessEventCore(eventData);
                break;

            case TelemetryMode.FireAndForget:
                _ = Task.Run(() => ProcessEventCore(eventData));
                break;

            case TelemetryMode.BackgroundChannel:
                EnqueueBackgroundEvent(eventData);
                break;

            default:
#pragma warning disable MA0015
                throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Invalid telemetry mode");
#pragma warning restore MA0015
        }
    }

    private void ProcessEventCore(TelemetryEvent eventData)
    {
        try
        {
            switch (eventData.Type)
            {
                case TelemetryEventType.ResultCreated:
                    ProcessResultCreated(eventData);
                    break;

                case TelemetryEventType.ResultTransformed:
                    ProcessResultTransformed(eventData);
                    break;

                case TelemetryEventType.Exception:
                    ProcessException(eventData);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(eventData),
                        eventData.Type,
                        "Unknown telemetry event type");
            }
        }
        catch (Exception ex)
        {
            // Don't let telemetry errors crash the application
            Debug.WriteLine($"Error processing telemetry event: {ex.Message}");
        }
    }

    private void EnqueueBackgroundEvent(TelemetryEvent eventData)
    {
        if (_channel is null)
        {
            ProcessEventCore(eventData);
            return;
        }

        var writer = _channel.Writer;

        if (writer.TryWrite(eventData))
        {
            return;
        }

        var writeTask = writer.WriteAsync(eventData, _cts?.Token ?? CancellationToken.None);

        if (writeTask.IsCompletedSuccessfully)
        {
            return;
        }

        try
        {
            writeTask.AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when telemetry is shutting down.
        }
        catch (ChannelClosedException)
        {
            // Ignore channel closed when telemetry is shutting down.
        }
    }

    private void ProcessResultCreated(TelemetryEvent eventData)
    {
        // Record metric
        _resultCreatedCounter.Add(1,
            new KeyValuePair<string, object?>("state", eventData.State.ToString()),
            new KeyValuePair<string, object?>("type", eventData.ResultType.Name),
            new KeyValuePair<string, object?>("caller", eventData.Caller ?? "Unknown"));

        // Update statistics if enabled
        if (_statistics != null)
        {
            var key = $"{eventData.ResultType.Name}:{eventData.State}";
            var stats = _statistics.GetOrAdd(key, _ => new PoolStatistics(key));
            stats.IncrementGets();
        }

        // Create activity for tracing (if active listener exists)
        if (ActivitySource.HasListeners())
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            using var activity = ActivitySource.StartActivity(name: "ResultCreated");
            activity?.SetTag("result.state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);
        }
    }

    private void ProcessResultTransformed(TelemetryEvent eventData)
    {
        // Record metric
        _resultTransformedCounter.Add(1,
            new KeyValuePair<string, object?>("original_state", eventData.OriginalState?.ToString() ?? "Unknown"),
            new KeyValuePair<string, object?>("new_state", eventData.State.ToString()),
            new KeyValuePair<string, object?>("type", eventData.ResultType.Name),
            new KeyValuePair<string, object?>("caller", eventData.Caller ?? "Unknown"));

        // Update statistics if enabled
        if (_statistics != null)
        {
            var key = $"{eventData.ResultType.Name}:Transform:{eventData.OriginalState}->{eventData.State}";
            var stats = _statistics.GetOrAdd(key, _ => new PoolStatistics(key));
            stats.IncrementGets();
        }

        // Create activity for tracing
        if (ActivitySource.HasListeners())
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            using var activity = ActivitySource.StartActivity(name: "ResultTransformed");
            activity?.SetTag("result.original_state", eventData.OriginalState?.ToString());
            activity?.SetTag("result.new_state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);
        }
    }

    private void ProcessException(TelemetryEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData.Exception, nameof(eventData));

        // Record metric
        _exceptionCounter.Add(1,
            new KeyValuePair<string, object?>("exception_type", eventData.Exception.GetType().Name),
            new KeyValuePair<string, object?>("state", eventData.State.ToString()),
            new KeyValuePair<string, object?>("type", eventData.ResultType.Name),
            new KeyValuePair<string, object?>("caller", eventData.Caller ?? "Unknown"));

        // Update statistics if enabled
        if (_statistics != null)
        {
            var key = $"{eventData.ResultType.Name}:Exception:{eventData.Exception.GetType().Name}";
            var stats = _statistics.GetOrAdd(key, _ => new PoolStatistics(key));
            stats.IncrementGets();
        }

        // Create activity for tracing
        if (ActivitySource.HasListeners())
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            using var activity = ActivitySource.StartActivity(name: "ResultException");
            activity?.SetTag("exception.type", eventData.Exception.GetType().Name);
            activity?.SetTag("exception.message", eventData.Exception.Message);
            activity?.SetTag("result.state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);

            // Add stack trace if configured
            if (_options.CaptureStackTraces)
            {
                activity?.SetTag("exception.stacktrace", eventData.Exception.StackTrace);
            }
        }
    }

    /// <summary>
    ///     Background processor for telemetry events (BackgroundChannel mode only).
    /// </summary>
    private async Task ProcessTelemetryEventsAsync(CancellationToken cancellationToken)
    {
        if (_channel == null)
        {
            return;
        }

        try
        {
            await foreach (var eventData in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                ProcessEventCore(eventData);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in telemetry background processor: {ex.Message}");
        }
    }

    #endregion

    #region Operation Duration Tracking

    /// <summary>
    ///     Records the duration of an operation.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="resultType">The result type involved in the operation.</param>
    /// <param name="caller">The caller member name.</param>
    /// <remarks>
    ///     This method is available for future use to track operation durations.
    ///     Currently not called internally but provides the infrastructure for timing-based metrics.
    /// </remarks>
    public void RecordOperationDuration(
        string operationName,
        double durationMs,
        Type resultType,
        [CallerMemberName] string? caller = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
        ArgumentNullException.ThrowIfNull(resultType, nameof(resultType));

        if (_mode == TelemetryMode.Disabled)
        {
            return;
        }

        _operationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("operation", operationName),
            new KeyValuePair<string, object?>("type", resultType.Name),
            new KeyValuePair<string, object?>("caller", caller ?? "Unknown"));
    }

    /// <summary>
    ///     Creates a disposable operation timer that automatically records duration when disposed.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="resultType">The result type involved in the operation.</param>
    /// <param name="caller">The caller member name.</param>
    /// <returns>A disposable that will record the operation duration when disposed.</returns>
    /// <remarks>
    ///     Usage example:
    ///     <code>
    ///     using (telemetry.StartOperation("Serialize", typeof(MyType)))
    ///     {
    ///         // Operation code here
    ///     } // Duration automatically recorded on dispose
    ///     </code>
    /// </remarks>
    public IDisposable StartOperation(
        string operationName,
        Type resultType,
        [CallerMemberName] string? caller = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
        ArgumentNullException.ThrowIfNull(resultType, nameof(resultType));

        return new OperationTimer(this, operationName, resultType, caller);
    }

    /// <summary>
    ///     Internal timer for tracking operation duration.
    /// </summary>
    private sealed class OperationTimer : IDisposable
    {
        private readonly string? _caller;
        private readonly string _operationName;
        private readonly Type _resultType;
        private readonly Stopwatch _stopwatch;
        private readonly DefaultResultTelemetry _telemetry;
        private bool _disposed;

        public OperationTimer(
            DefaultResultTelemetry telemetry,
            string operationName,
            Type resultType,
            string? caller)
        {
            _telemetry = telemetry;
            _operationName = operationName;
            _resultType = resultType;
            _caller = caller;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            _telemetry.RecordOperationDuration(
                _operationName,
                _stopwatch.Elapsed.TotalMilliseconds,
                _resultType,
                _caller);
        }
    }

    #endregion

    #region Statistics

    /// <summary>
    ///     Gets all collected statistics.
    ///     Returns null if statistics collection is disabled.
    /// </summary>
    public IReadOnlyDictionary<string, PoolStatistics>? GetStatistics() => _statistics;

    /// <summary>
    ///     Clears all collected statistics.
    /// </summary>
    public void ClearStatistics()
    {
        if (_statistics != null)
        {
            foreach (var stats in _statistics.Values)
            {
                stats.Reset();
            }
        }
    }

    #endregion
}
