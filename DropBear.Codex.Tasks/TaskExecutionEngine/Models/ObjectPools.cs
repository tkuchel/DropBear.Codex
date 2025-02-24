#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;
using IResettable = DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces.IResettable;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     A generic static pool for objects of type <typeparamref name="T" /> using a <see cref="ConcurrentQueue{T}" />.
///     Optionally uses a factory method and calls <see cref="IResettable.Reset" /> when returning an object.
/// </summary>
internal static class ObjectPools<T> where T : class, new()
{
    private const int MaxPoolSize = 1024;
    private static readonly ConcurrentQueue<T> Pool = new();
    private static int _count;
    private static Func<T>? _factory;

    /// <summary>
    ///     Creates a <see cref="DefaultObjectPool{T}" /> using the specified factory for new objects.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectPool<T> CreateWithFactory(Func<T> factory)
    {
        _factory = factory;
        return new DefaultObjectPool<T>(new PooledObjectPolicy());
    }

    /// <summary>
    ///     Gets (or "rents") an instance from the pool, or creates a new one if the pool is empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Rent()
    {
        if (Pool.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _count);
            return item;
        }

        return _factory?.Invoke() ?? new T();
    }

    /// <summary>
    ///     Returns an instance to the pool, resetting it if it implements <see cref="IResettable" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T? item)
    {
        if (item == null || _count >= MaxPoolSize)
        {
            return;
        }

        if (Interlocked.Increment(ref _count) > MaxPoolSize)
        {
            Interlocked.Decrement(ref _count);
            return;
        }

        if (item is IResettable resettable)
        {
            resettable.Reset();
        }

        Pool.Enqueue(item);
    }

    private sealed class PooledObjectPolicy : IPooledObjectPolicy<T>
    {
        public T Create()
        {
            return Rent();
        }

        public bool Return(T obj)
        {
            ObjectPools<T>.Return(obj);
            return true;
        }
    }
}
