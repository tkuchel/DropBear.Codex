#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
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
                throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Invalid telemetry mode");
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
                    throw new ArgumentOutOfRangeException(nameof(eventData.Type), eventData.Type,
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
            using var activity = ActivitySource.StartActivity("ResultCreated");
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
            using var activity = ActivitySource.StartActivity("ResultTransformed");
            activity?.SetTag("result.original_state", eventData.OriginalState?.ToString());
            activity?.SetTag("result.new_state", eventData.State.ToString());
            activity?.SetTag("result.type", eventData.ResultType.Name);
            activity?.SetTag("caller", eventData.Caller);
        }
    }

    private void ProcessException(TelemetryEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData.Exception);

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
            using var activity = ActivitySource.StartActivity("ResultException");
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

#region Supporting Types

/// <summary>
///     Represents a telemetry event to be processed.
/// </summary>
internal readonly record struct TelemetryEvent
{
    public required TelemetryEventType Type { get; init; }
    public required ResultState State { get; init; }
    public ResultState? OriginalState { get; init; }
    public required Type ResultType { get; init; }
    public Exception? Exception { get; init; }
    public string? Caller { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
///     Defines the type of telemetry event.
/// </summary>
internal enum TelemetryEventType
{
    ResultCreated,
    ResultTransformed,
    Exception
}

/// <summary>
///     Tracks statistics for telemetry operations.
///     Reused from ObjectPoolManager for consistency.
/// </summary>
public sealed class PoolStatistics
{
    private long _totalGets;
    private long _totalReturns;

    public PoolStatistics(string typeName)
    {
        TypeName = typeName;
    }

    public string TypeName { get; }
    public long TotalGets => Interlocked.Read(ref _totalGets);
    public long TotalReturns => Interlocked.Read(ref _totalReturns);
    public double ReturnRate => TotalGets > 0 ? (double)TotalReturns / TotalGets : 1.0;
    public long OutstandingObjects => TotalGets - TotalReturns;

    internal void IncrementGets() => Interlocked.Increment(ref _totalGets);
    internal void IncrementReturns() => Interlocked.Increment(ref _totalReturns);

    internal void Reset()
    {
        Interlocked.Exchange(ref _totalGets, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
    }

    public override string ToString()
    {
        return $"{TypeName}: Gets={TotalGets}, Returns={TotalReturns}, " +
               $"Rate={ReturnRate:P0}, Outstanding={OutstandingObjects}";
    }
}

#endregion
