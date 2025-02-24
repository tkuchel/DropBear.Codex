#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Enhanced pool policy for HashSet<T> with size tracking and custom comparer support
/// </summary>
internal sealed class EnhancedHashSetObjectPoolPolicy<T> : PooledObjectPolicy<HashSet<T>>
{
    private const int DefaultCapacity = 32;
    private readonly IEqualityComparer<T>? _comparer;
    private readonly ConditionalWeakTable<HashSet<T>, object?> _sizes;

    public EnhancedHashSetObjectPoolPolicy(
        ConditionalWeakTable<HashSet<T>, object?> sizes,
        IEqualityComparer<T>? comparer = null)
    {
        _sizes = sizes;
        _comparer = comparer;
    }

    public override HashSet<T> Create()
    {
        return _comparer != null ? new HashSet<T>(DefaultCapacity, _comparer) : new HashSet<T>(DefaultCapacity);
    }

    public override bool Return(HashSet<T>? obj)
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
