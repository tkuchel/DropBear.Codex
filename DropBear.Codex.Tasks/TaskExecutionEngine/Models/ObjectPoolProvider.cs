#region

using System.Numerics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Provides optimized object pooling for collection types with improved memory management
/// </summary>
internal static class ObjectPoolProvider
{
    private static readonly DefaultObjectPoolProvider Provider = new() { MaximumRetained = 1024 };

    // Collection pools with size tracking
    private static readonly ConditionalWeakTable<List<ITask>, object?> TaskListSizes = new();
    private static readonly ConditionalWeakTable<HashSet<string>, object?> StringSetSizes = new();
    private static readonly ConditionalWeakTable<Dictionary<string, bool>, object?> BoolDictSizes = new();

    // Cached delegate for initial capacity calculation
    private static readonly Func<int, int> CalculateCapacity = size =>
    {
        if (size <= 32)
        {
            return 32;
        }

        return (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(size, Array.MaxLength));
    };

    public static readonly ObjectPool<List<ITask>> TaskListPool =
        Provider.Create<List<ITask>>(new EnhancedListObjectPoolPolicy<ITask>(TaskListSizes));

    public static readonly ObjectPool<HashSet<string>> StringSetPool =
        Provider.Create(new EnhancedHashSetObjectPoolPolicy<string>(StringSetSizes, StringComparer.Ordinal));

    public static readonly ObjectPool<Dictionary<string, bool>> BoolDictionaryPool =
        Provider.Create(new EnhancedDictionaryPoolPolicy<string, bool>(BoolDictSizes, StringComparer.Ordinal));

    /// <summary>
    ///     Rents a List with an initial capacity based on the expected size
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<ITask> RentTaskList(int expectedSize = 32)
    {
        var List = TaskListPool.Get();
        TaskListSizes.AddOrUpdate(List, CalculateCapacity(expectedSize));
        return List;
    }

    /// <summary>
    ///     Rents a HashSet with an initial capacity based on the expected size
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<string> RentStringSet(int expectedSize = 32)
    {
        var set = StringSetPool.Get();
        StringSetSizes.AddOrUpdate(set, CalculateCapacity(expectedSize));
        return set;
    }

    /// <summary>
    ///     Rents a Dictionary with an initial capacity based on the expected size
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, bool> RentBoolDictionary(int expectedSize = 32)
    {
        var dict = BoolDictionaryPool.Get();
        BoolDictSizes.AddOrUpdate(dict, CalculateCapacity(expectedSize));
        return dict;
    }
}
