#region

using System.Collections.Concurrent;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public static class ObjectPools<T> where T : class, new()
{
    private const int MaxPoolSize = 1000;
    private static readonly ConcurrentQueue<T> Pool = new();

    public static T Rent()
    {
        return Pool.TryDequeue(out var item) ? item : new T();
    }

    public static void Return(T item)
    {
        if (Pool.Count < MaxPoolSize)
        {
            Pool.Enqueue(item);
        }
    }
}

/// <summary>
///     Manages object pooling for various collection types
/// </summary>
internal static class ObjectPoolProvider
{
    private static readonly DefaultObjectPoolProvider Provider = new();

    public static readonly ObjectPool<List<ITask>> TaskListPool =
        Provider.Create(new ListObjectPoolPolicy<ITask>());

    public static readonly ObjectPool<HashSet<string>> StringSetPool =
        Provider.Create(new HashSetObjectPoolPolicy<string>());

    public static readonly ObjectPool<Dictionary<string, bool>> BoolDictionaryPool =
        Provider.Create(new DictionaryPoolPolicy<string, bool>());
}

/// <summary>
///     Custom pool policies for collections
/// </summary>
internal sealed class ListObjectPoolPolicy<T> : PooledObjectPolicy<List<T>>
{
    public override List<T> Create()
    {
        return new List<T>(32);
    }

    public override bool Return(List<T> obj)
    {
        obj.Clear();
        return true;
    }
}

internal sealed class HashSetObjectPoolPolicy<T> : PooledObjectPolicy<HashSet<T>>
{
    public override HashSet<T> Create()
    {
        return new HashSet<T>(32);
    }

    public override bool Return(HashSet<T> obj)
    {
        obj.Clear();
        return true;
    }
}

internal sealed class DictionaryPoolPolicy<TKey, TValue> : PooledObjectPolicy<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    public override Dictionary<TKey, TValue> Create()
    {
        return new Dictionary<TKey, TValue>(32);
    }

    public override bool Return(Dictionary<TKey, TValue> obj)
    {
        obj.Clear();
        return true;
    }
}
