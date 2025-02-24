#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Enhanced pool policy for Dictionary<TKey, TValue> with size tracking and custom comparer support
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

            // Future optimization: Consider implementing TrimExcess if size is significantly smaller
            return true;
        }
        catch
        {
            return false;
        }
    }
}
