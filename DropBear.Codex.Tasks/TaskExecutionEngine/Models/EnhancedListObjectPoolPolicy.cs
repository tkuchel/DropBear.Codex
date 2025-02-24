#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Enhanced pool policy for <see cref="List{T}" /> with size tracking and optimized reuse.
/// </summary>
internal sealed class EnhancedListObjectPoolPolicy<T> : PooledObjectPolicy<List<T>>
{
    private const int DefaultCapacity = 32;
    private readonly ConditionalWeakTable<List<T>, object?> _sizes;

    public EnhancedListObjectPoolPolicy(ConditionalWeakTable<List<T>, object?> sizes)
    {
        _sizes = sizes;
    }

    public override List<T> Create()
    {
        return new List<T>(DefaultCapacity);
    }

    public override bool Return(List<T> obj)
    {
        if (obj == null)
        {
            return false;
        }

        try
        {
            obj.Clear();

            // If we have a recorded "ideal capacity," set it back.
            if (_sizes.TryGetValue(obj, out var sizeObj) &&
                sizeObj is int size &&
                obj.Capacity != size)
            {
                obj.Capacity = size;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
