#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Captures task execution events with optional real-time streaming support.
///     Provides both buffered storage for historical events and streaming for live monitoring.
/// </summary>
public sealed class TaskExecutionTrace : IDisposable
{
    private readonly ConcurrentQueue<TaskExecutionEvent> _buffer;
    private readonly int _capacity;
    private readonly Channel<TaskExecutionEvent>? _streamingChannel;
    private int _count;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskExecutionTrace" /> class.
    /// </summary>
    /// <param name="capacity">Maximum number of events to buffer in memory</param>
    /// <param name="enableStreaming">If true, enables real-time streaming via IAsyncEnumerable</param>
    public TaskExecutionTrace(int capacity = 1000, bool enableStreaming = false)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _buffer = new ConcurrentQueue<TaskExecutionEvent>();
        _count = 0;

        if (enableStreaming)
        {
            _streamingChannel = Channel.CreateUnbounded<TaskExecutionEvent>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }
    }

    /// <summary>
    ///     Gets the current number of events in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    ///     Gets whether streaming is enabled for this trace.
    /// </summary>
    public bool IsStreamingEnabled => _streamingChannel is not null;

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CompleteStreaming();
    }

    /// <summary>
    ///     Adds an execution event to the trace.
    ///     If streaming is enabled, the event is also written to the streaming channel.
    /// </summary>
    /// <param name="event">The execution event to add</param>
    public void Add(TaskExecutionEvent @event)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Add to circular buffer
        _buffer.Enqueue(@event);
        Interlocked.Increment(ref _count);

        // Maintain capacity by removing oldest events
        while (_buffer.Count > _capacity)
        {
            if (_buffer.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }

        // Stream the event if streaming is enabled
        if (_streamingChannel is not null)
        {
            // Use TryWrite instead of WriteAsync to avoid blocking
            // If channel is full, we prioritize execution over streaming
            _streamingChannel.Writer.TryWrite(@event);
        }
    }

    /// <summary>
    ///     Gets all currently buffered events as a snapshot.
    /// </summary>
    /// <returns>A collection of all buffered execution events</returns>
    public IReadOnlyCollection<TaskExecutionEvent> GetAll()
    {
        return _buffer.ToArray();
    }

    /// <summary>
    ///     Clears all buffered events from the trace.
    /// </summary>
    public void Clear()
    {
        while (_buffer.TryDequeue(out _))
        {
        }

        _count = 0;
    }

    /// <summary>
    ///     Streams execution events as they are added in real-time.
    ///     This method will continue yielding items until the cancellation token is triggered
    ///     or the stream is completed.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the streaming operation</param>
    /// <returns>An async enumerable of execution events</returns>
    /// <exception cref="InvalidOperationException">Thrown when streaming is not enabled</exception>
    public async IAsyncEnumerable<TaskExecutionEvent> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_streamingChannel is null)
        {
            throw new InvalidOperationException(
                "Streaming is not enabled. Create the TaskExecutionTrace with enableStreaming=true.");
        }

        await foreach (var @event in _streamingChannel.Reader.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return @event;
        }
    }

    /// <summary>
    ///     Completes the streaming channel, signaling that no more events will be added.
    ///     Call this when task execution is complete.
    /// </summary>
    public void CompleteStreaming()
    {
        _streamingChannel?.Writer.Complete();
    }

    /// <summary>
    ///     Completes the streaming channel with an exception.
    ///     Call this when task execution fails.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    public void CompleteStreaming(Exception exception)
    {
        _streamingChannel?.Writer.Complete(exception);
    }
}
