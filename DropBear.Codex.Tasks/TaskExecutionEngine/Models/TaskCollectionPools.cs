#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion


namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Provides pooled collections for common task execution scenarios
/// </summary>
internal static class TaskCollectionPools
{
    private const int DefaultCapacity = 32;

    // Strongly-typed pools for common task types
    private static readonly ObjectPool<List<ITask>> TaskListPool = CreateListPool<ITask>();
    private static readonly ObjectPool<HashSet<string>> StringSetPool = CreateSetPool();
    private static readonly ObjectPool<Dictionary<string, bool>> BoolDictionaryPool = CreateDictionaryPool<bool>();

    private static readonly ObjectPool<Dictionary<string, TaskExecutionMetrics>> MetricsPool =
        CreateDictionaryPool<TaskExecutionMetrics>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<ITask> RentTaskList(int? capacity = null)
    {
        var List = TaskListPool.Get();
        if (capacity.HasValue && List.Capacity < capacity.Value)
        {
            List.Capacity = capacity.Value;
        }

        return List;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> RentList<T>(int? capacity = null)
    {
        var List = new List<T>(capacity ?? DefaultCapacity);
        return List;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<string> RentStringSet()
    {
        return StringSetPool.Get();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, bool> RentBoolDictionary()
    {
        return BoolDictionaryPool.Get();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, TaskExecutionMetrics> RentMetricsDictionary()
    {
        return MetricsPool.Get();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(List<T>? List)
    {
        if (List == null)
        {
            return;
        }

        List.Clear();

        // Only return to pool if it's our specialized ITask List
        if (typeof(T) == typeof(ITask))
        {
            ObjectPools<List<ITask>>.Return(List as List<ITask>);
        }
    }

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
        return ObjectPools<HashSet<string>>.CreateWithFactory(() =>
            new HashSet<string>(DefaultCapacity, StringComparer.Ordinal));
    }

    private static ObjectPool<Dictionary<string, T>> CreateDictionaryPool<T>()
    {
        return ObjectPools<Dictionary<string, T>>.CreateWithFactory(() =>
            new Dictionary<string, T>(DefaultCapacity, StringComparer.Ordinal));
    }
}
