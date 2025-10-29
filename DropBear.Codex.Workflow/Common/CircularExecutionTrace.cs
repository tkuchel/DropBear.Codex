#region

using DropBear.Codex.Workflow.Metrics;

#endregion

namespace DropBear.Codex.Workflow.Common;

/// <summary>
///     Circular buffer for execution trace entries that maintains a fixed capacity
///     and automatically overwrites oldest entries when full.
/// </summary>
/// <typeparam name="T">The type of trace entries</typeparam>
public sealed class CircularExecutionTrace<T>
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private int _count;
    private int _head;
    private int _tail;

    /// <summary>
    ///     Initializes a new instance of the circular execution trace.
    /// </summary>
    /// <param name="capacity">Maximum number of entries to store</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 1</exception>
    public CircularExecutionTrace(int capacity)
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
    ///     Adds an item to the trace buffer. If the buffer is full,
    ///     the oldest item will be overwritten.
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
}
