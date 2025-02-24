#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Enhanced pool policy for <see cref="HashSet{T}" />, clearing on return.
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
        return _comparer != null
            ? new HashSet<T>(DefaultCapacity, _comparer)
            : new HashSet<T>(DefaultCapacity);
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
            // Optionally trim or reset capacity in .NET 7 using obj.TrimExcess(DesiredCapacity);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
