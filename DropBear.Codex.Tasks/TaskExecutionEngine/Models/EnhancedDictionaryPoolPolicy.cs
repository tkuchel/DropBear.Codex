#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Enhanced pool policy for <see cref="Dictionary{TKey, TValue}" />
///     with optional custom comparer. Clears on return.
/// </summary>
internal sealed class EnhancedDictionaryPoolPolicy<TKey, TValue> : PooledObjectPolicy<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    private const int DefaultCapacity = 32;
    private readonly IEqualityComparer<TKey>? _comparer;
    private readonly ConditionalWeakTable<Dictionary<TKey, TValue>, object?> _sizes;

    public EnhancedDictionaryPoolPolicy(
        ConditionalWeakTable<Dictionary<TKey, TValue>, object?> sizes,
        IEqualityComparer<TKey>? comparer = null)
    {
        _sizes = sizes;
        _comparer = comparer;
    }

    public override Dictionary<TKey, TValue> Create()
    {
        return _comparer != null
            ? new Dictionary<TKey, TValue>(DefaultCapacity, _comparer)
            : new Dictionary<TKey, TValue>(DefaultCapacity);
    }

    public override bool Return(Dictionary<TKey, TValue>? obj)
    {
        if (obj == null)
        {
            return false;
        }

        try
        {
            obj.Clear();

            // *** CHANGE *** Potential approach to shrink dictionary if it's too big (requires .NET 7+):
            // if (obj.Count == 0 && obj.EnsureCapacity(...) > DesiredCapacity)
            // {
            //     obj.TrimExcess(DesiredCapacity);
            // }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
