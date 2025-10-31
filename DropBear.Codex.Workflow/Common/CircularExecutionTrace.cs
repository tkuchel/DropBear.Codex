#region

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Workflow.Metrics;

#endregion

namespace DropBear.Codex.Workflow.Common;

/// <summary>
///     Circular buffer for execution trace entries that maintains a fixed capacity
///     and automatically overwrites oldest entries when full.
///     Supports real-time streaming of traces via IAsyncEnumerable.
/// </summary>
/// <typeparam name="T">The type of trace entries</typeparam>
public sealed class CircularExecutionTrace<T>
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private readonly Channel<T>? _streamingChannel;
    private int _count;
    private int _head;
    private int _tail;

    /// <summary>
    ///     Initializes a new instance of the circular execution trace.
    /// </summary>
    /// <param name="capacity">Maximum number of entries to store</param>
    /// <param name="enableStreaming">Whether to enable real-time streaming of traces</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 1</exception>
    public CircularExecutionTrace(int capacity, bool enableStreaming = false)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1");
        }

        _capacity = capacity;
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;

        // Initialize streaming channel if enabled
        if (enableStreaming)
        {
            var options = new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };
            _streamingChannel = Channel.CreateUnbounded<T>(options);
        }
    }

    /// <summary>
    ///     Gets the current number of entries in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    ///     Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    ///     Gets a value indicating whether the buffer is full.
    ///     When true, adding new items will overwrite the oldest entries.
    /// </summary>
    public bool IsFull => _count == _capacity;

    /// <summary>
    ///     Gets a value indicating whether real-time streaming is enabled.
    /// </summary>
    public bool IsStreamingEnabled => _streamingChannel is not null;

    /// <summary>
    ///     Adds an item to the trace buffer. If the buffer is full,
    ///     the oldest item will be overwritten.
    ///     If streaming is enabled, the item is also published to the stream.
    /// </summary>
    /// <param name="item">The item to add</param>
    public void Add(T item)
    {
        if (_count == _capacity)
        {
            // Overwrite oldest entry
            _buffer[_tail] = item;
            _tail = (_tail + 1) % _capacity;
            _head = (_head + 1) % _capacity;
        }
        else
        {
            // Add to next available position
            _buffer[_tail] = item;
            _tail = (_tail + 1) % _capacity;
            _count++;
        }

        // Publish to streaming channel if enabled
        // Use TryWrite for non-blocking operation
        _streamingChannel?.Writer.TryWrite(item);
    }

    /// <summary>
    ///     Converts the circular buffer to a list in chronological order (oldest to newest).
    /// </summary>
    /// <returns>A read-only list of trace entries</returns>
    public IReadOnlyList<T> ToList()
    {
        var result = new List<T>(_count);
        for (var i = 0; i < _count; i++)
        {
            result.Add(_buffer[(_head + i) % _capacity]);
        }

        return result;
    }

    /// <summary>
    ///     Clears all entries from the buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    ///     Streams trace entries as they are added in real-time.
    ///     This method will continue yielding items until the cancellation token is triggered
    ///     or the stream is completed.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the streaming operation</param>
    /// <returns>An async enumerable of trace entries</returns>
    /// <exception cref="InvalidOperationException">Thrown when streaming is not enabled</exception>
    public async IAsyncEnumerable<T> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_streamingChannel is null)
        {
            throw new InvalidOperationException(
                "Streaming is not enabled. Create the CircularExecutionTrace with enableStreaming=true.");
        }

        await foreach (var item in _streamingChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    ///     Completes the streaming channel, signaling that no more items will be added.
    ///     Call this when the workflow execution is complete.
    /// </summary>
    public void CompleteStreaming()
    {
        _streamingChannel?.Writer.Complete();
    }

    /// <summary>
    ///     Completes the streaming channel with an exception.
    ///     Call this when the workflow execution fails.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    public void CompleteStreaming(Exception exception)
    {
        _streamingChannel?.Writer.Complete(exception);
    }
}
