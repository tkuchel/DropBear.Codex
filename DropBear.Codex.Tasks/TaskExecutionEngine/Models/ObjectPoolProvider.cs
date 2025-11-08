#region

using System.Numerics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Provides specialized object pools for common collection types with size tracking.
/// </summary>
internal static class ObjectPoolProvider
{
    private static readonly DefaultObjectPoolProvider Provider = new() { MaximumRetained = 1024 };

    // Conditional tables to track "ideal" size or capacity
    private static readonly ConditionalWeakTable<List<ITask>, object?> TaskListSizes = new();
    private static readonly ConditionalWeakTable<HashSet<string>, object?> StringSetSizes = new();
    private static readonly ConditionalWeakTable<Dictionary<string, bool>, object?> BoolDictSizes = new();

    // Helper for rounding up capacity
    private static readonly Func<int, int> CalculateCapacity = size =>
    {
        if (size <= 32)
        {
            return 32;
        }

        // Round up to next power of 2 but don't exceed array limits
        return (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(size, Array.MaxLength));
    };

    public static readonly ObjectPool<List<ITask>> TaskListPool =
        Provider.Create(new EnhancedListObjectPoolPolicy<ITask>(TaskListSizes));

    public static readonly ObjectPool<HashSet<string>> StringSetPool =
        Provider.Create(new EnhancedHashSetObjectPoolPolicy<string>(StringSetSizes, StringComparer.Ordinal));

    public static readonly ObjectPool<Dictionary<string, bool>> BoolDictionaryPool =
        Provider.Create(new EnhancedDictionaryPoolPolicy<string, bool>(BoolDictSizes, StringComparer.Ordinal));

    /// <summary>
    ///     Rents a <see cref="List{ITask}" /> with a capacity approximated from the <paramref name="expectedSize" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<ITask> RentTaskList(int expectedSize = 32)
    {
        var list = TaskListPool.Get();
        TaskListSizes.AddOrUpdate(list, CalculateCapacity(expectedSize));
        return list;
    }

    /// <summary>
    ///     Rents a <see cref="HashSet{T}" /> with a capacity approximated from the <paramref name="expectedSize" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<string> RentStringSet(int expectedSize = 32)
    {
        var set = StringSetPool.Get();
        StringSetSizes.AddOrUpdate(set, CalculateCapacity(expectedSize));
        return set;
    }

    /// <summary>
    ///     Rents a <see cref="Dictionary{TKey, TValue}" /> with a capacity approximated from the
    ///     <paramref name="expectedSize" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, bool> RentBoolDictionary(int expectedSize = 32)
    {
        var dict = BoolDictionaryPool.Get();
        BoolDictSizes.AddOrUpdate(dict, CalculateCapacity(expectedSize));
        return dict;
    }
}
