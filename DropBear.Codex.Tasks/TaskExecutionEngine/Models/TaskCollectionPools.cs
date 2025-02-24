#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Provides pooled collections (lists, sets, dictionaries) for task execution.
/// </summary>
internal static class TaskCollectionPools
{
    private const int DefaultCapacity = 32;

    // Strongly-typed pools for common types
    private static readonly ObjectPool<List<ITask>> TaskListPool = CreateListPool<ITask>();
    private static readonly ObjectPool<HashSet<string>> StringSetPool = CreateSetPool();
    private static readonly ObjectPool<Dictionary<string, bool>> BoolDictionaryPool = CreateDictionaryPool<bool>();

    private static readonly ObjectPool<Dictionary<string, TaskExecutionMetrics>> MetricsPool =
        CreateDictionaryPool<TaskExecutionMetrics>();

    /// <summary>
    ///     Rents a pooled <see cref="List{ITask}" />, optionally ensuring it has at least <paramref name="capacity" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<ITask> RentTaskList(int? capacity = null)
    {
        var list = TaskListPool.Get();
        if (capacity.HasValue && list.Capacity < capacity.Value)
        {
            list.Capacity = capacity.Value;
        }

        return list;
    }

    /// <summary>
    ///     Returns a non-pooled <see cref="List{T}" />.
    ///     This is used for ephemeral lists that aren't specialized for <see cref="ITask" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> RentList<T>(int? capacity = null)
    {
        // Not actually from a pool; just returns a new List.
        return new List<T>(capacity ?? DefaultCapacity);
    }

    /// <summary>
    ///     Rents a pooled <see cref="HashSet{string}" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<string> RentStringSet()
    {
        return StringSetPool.Get();
    }

    /// <summary>
    ///     Rents a pooled <see cref="Dictionary{string, bool}" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, bool> RentBoolDictionary()
    {
        return BoolDictionaryPool.Get();
    }

    /// <summary>
    ///     Rents a pooled <see cref="Dictionary{string, TaskExecutionMetrics}" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, TaskExecutionMetrics> RentMetricsDictionary()
    {
        return MetricsPool.Get();
    }

    /// <summary>
    ///     Returns a list. If it's <see cref="List{ITask}" />, it will be returned to the specialized pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(List<T>? list)
    {
        if (list == null)
        {
            return;
        }

        list.Clear();

        // Only return to the specialized pool if T == ITask
        if (typeof(T) == typeof(ITask))
        {
            ObjectPools<List<ITask>>.Return(list as List<ITask>);
        }
        // Otherwise do nothing
    }

    /// <summary>
    ///     Clears and returns a <see cref="HashSet{string}" /> to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(HashSet<string>? set)
    {
        if (set == null)
        {
            return;
        }

        set.Clear();
        ObjectPools<HashSet<string>>.Return(set);
    }

    /// <summary>
    ///     Clears and returns a <see cref="Dictionary{string, TValue}" /> to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<TValue>(Dictionary<string, TValue>? dict)
    {
        if (dict == null)
        {
            return;
        }

        dict.Clear();
        ObjectPools<Dictionary<string, TValue>>.Return(dict);
    }

    private static ObjectPool<List<T>> CreateListPool<T>()
    {
        return ObjectPools<List<T>>.CreateWithFactory(() => new List<T>(DefaultCapacity));
    }

    private static ObjectPool<HashSet<string>> CreateSetPool()
    {
        return ObjectPools<HashSet<string>>.CreateWithFactory(
            () => new HashSet<string>(DefaultCapacity, StringComparer.Ordinal));
    }

    private static ObjectPool<Dictionary<string, T>> CreateDictionaryPool<T>()
    {
        return ObjectPools<Dictionary<string, T>>.CreateWithFactory(
            () => new Dictionary<string, T>(DefaultCapacity, StringComparer.Ordinal));
    }
}
