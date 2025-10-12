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
public sealed class DefaultResultTelemetry : IResultTelemetry, IAsyncDisposable, IDisposable
{
    // Instance-level resources (not static!) for proper disposal
    private readonly ActivitySource _activitySource;

    // Channel-based processing (for BackgroundChannel mode)
    private readonly Channel<TelemetryEvent>? _channel;
    private readonly CancellationTokenSource? _cts;
    private readonly Counter<long> _exceptionCounter;
    private readonly Meter _meter;
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

        // Create instance-specific ActivitySource and Meter (not static!)
        _activitySource = new ActivitySource("DropBear.Codex.Core.Results", "2.0.0");
        _meter = new Meter("DropBear.Codex.Core.Results", "2.0.0");

        // Initialize metrics using instance meter
        _resultCreatedCounter = _meter.CreateCounter<long>(
            "results.created",
            description: "Number of results created");

        _resultTransformedCounter = _meter.CreateCounter<long>(
            "results.transformed",
            description: "Number of result transformations");

        _exceptionCounter = _meter.CreateCounter<long>(
            "results.exceptions",
            description: "Number of exceptions in results");

        _operationDuration = _meter.CreateHistogram<double>(
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

    #region IAsyncDisposable Implementation

    /// <summary>
    ///     Asynchronously disposes the telemetry instance and stops background processing.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal cancellation for background processor
        _cts?.CancelAsync();

        // Complete the channel to signal no more writes
        _channel?.Writer.Complete();

        // Await processor task asynchronously (no blocking!)
        if (_processorTask != null)
        {
            try
            {
                await _processorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error waiting for telemetry processor: {ex.Message}");
            }
        }

        // Dispose resources owned by this instance
        _cts?.Dispose();
        _activitySource.Dispose();
        _meter.Dispose();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    ///     Synchronously disposes the telemetry instance and stops background processing.
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
                var completed = _processorTask.Wait(TimeSpan.FromSeconds(5));
                if (!completed)
                {
                    Debug.WriteLine("Warning: Telemetry processor did not complete within timeout");
                }
            }
            catch (AggregateException)
            {
                // Expected - ignore cancellation exceptions
            }
        }

        // Dispose resources owned by this instance
        _cts?.Dispose();
        _activitySource.Dispose();
        _meter.Dispose();
    }

    #endregion

    #region IResultTelemetry Implementation

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackResultCreated(ResultState state, Type resultType, string? caller = null)
    {
        if (_mode == TelemetryMode.Disabled || _disposed)
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
        if (_mode == TelemetryMode.Disabled || _disposed)
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
        if (_mode == TelemetryMode.Disabled || _disposed)
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
        if (_disposed)
        {
            return;
        }

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
        if (_disposed)
        {
            return;
        }

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

    /// <summary>
    ///     Enqueues an event to the background channel, respecting the configured FullMode.
    ///     FIXED: Now properly honors BoundedChannelFullMode.Wait configuration.
    /// </summary>
    private void EnqueueBackgroundEvent(TelemetryEvent eventData)
    {
        if (_channel is null || _disposed)
        {
            // Fallback to synchronous processing if channel unavailable
            ProcessEventCore(eventData);
            return;
        }

        var writer = _channel.Writer;

        // FIXED: Check the configured FullMode to determine write behavior
        if (_options.FullMode == ChannelFullMode.Wait)
        {
            // For Wait mode, use WriteAsync to respect backpressure
            // Fire and forget the write task, but handle completion
            _ = Task.Run(async () =>
            {
                try
                {
                    await writer.WriteAsync(eventData, _cts?.Token ?? CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (ChannelClosedException)
                {
                    // Expected if disposed
                }
            });
        }
        else
        {
            // For DropOldest and DropNewest, TryWrite is appropriate
            // This allows dropping events when channel is full
            if (!writer.TryWrite(eventData))
            {
                // Event was dropped due to full channel
                Debug.WriteLine($"Telemetry event dropped: {eventData.Type}");
            }
        }
    }

    /// <summary>
    ///     Background processor that reads events from the channel.
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

    #region Event Processors

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
        if (_activitySource.HasListeners())
        {
            using var activity = _activitySource.StartActivity("ResultCreated");
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
            var key = $"{eventData.ResultType.Name}:Transform";
            var stats = _statistics.GetOrAdd(key, _ => new PoolStatistics(key));
            stats.IncrementGets();
        }

        // Create activity for tracing
        if (_activitySource.HasListeners())
        {
            using var activity = _activitySource.StartActivity("ResultTransformed");
            activity?.SetTag("original.state", eventData.OriginalState?.ToString());
            activity?.SetTag("new.state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);
        }
    }

    private void ProcessException(TelemetryEvent eventData)
    {
        if (eventData.Exception == null)
        {
            return;
        }

        // Record metric
        _exceptionCounter.Add(1,
            new KeyValuePair<string, object?>("exception.type", eventData.Exception.GetType().Name),
            new KeyValuePair<string, object?>("state", eventData.State.ToString()),
            new KeyValuePair<string, object?>("result.type", eventData.ResultType.Name),
            new KeyValuePair<string, object?>("caller", eventData.Caller ?? "Unknown"));

        // Update statistics if enabled
        if (_statistics != null)
        {
            var key = $"{eventData.ResultType.Name}:Exception";
            var stats = _statistics.GetOrAdd(key, _ => new PoolStatistics(key));
            stats.IncrementGets();
        }

        // Create activity for tracing with exception details
        if (_activitySource.HasListeners())
        {
            using var activity = _activitySource.StartActivity("ResultException");
            activity?.SetTag("exception.type", eventData.Exception.GetType().Name);
            activity?.SetTag("exception.message", eventData.Exception.Message);
            activity?.SetTag("result.state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);

            // Record exception event
            activity?.AddEvent(new ActivityEvent("exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", eventData.Exception.GetType().FullName },
                    { "exception.message", eventData.Exception.Message },
                    { "exception.stacktrace", eventData.Exception.StackTrace }
                }));
        }
    }

    #endregion

    #region Operation Duration Tracking

    /// <summary>
    ///     Records the duration of an operation.
    /// </summary>
    public void RecordOperationDuration(
        string operationName,
        double durationMs,
        Type resultType,
        [CallerMemberName] string? caller = null)
    {
        if (_disposed || _mode == TelemetryMode.Disabled)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
        ArgumentNullException.ThrowIfNull(resultType, nameof(resultType));

        _operationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("operation", operationName),
            new KeyValuePair<string, object?>("type", resultType.Name),
            new KeyValuePair<string, object?>("caller", caller ?? "Unknown"));
    }

    /// <summary>
    ///     Creates a disposable operation timer.
    /// </summary>
    public IDisposable StartOperation(
        string operationName,
        Type resultType,
        [CallerMemberName] string? caller = null)
    {
        if (_disposed || _mode == TelemetryMode.Disabled)
        {
            return NullDisposable.Instance;
        }

        return new OperationTimer(this, operationName, resultType, caller);
    }

    #endregion

    #region Helper Types

    /// <summary>
    ///     A disposable timer that records operation duration.
    /// </summary>
    private sealed class OperationTimer : IDisposable
    {
        private readonly string? _caller;
        private readonly string _operationName;
        private readonly Type _resultType;
        private readonly Stopwatch _stopwatch;
        private readonly DefaultResultTelemetry _telemetry;

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
            _stopwatch.Stop();
            _telemetry.RecordOperationDuration(
                _operationName,
                _stopwatch.Elapsed.TotalMilliseconds,
                _resultType,
                _caller);
        }
    }

    /// <summary>
    ///     A no-op disposable for when telemetry is disabled.
    /// </summary>
    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        private NullDisposable() { }
        public void Dispose() { }
    }

    #endregion
}
