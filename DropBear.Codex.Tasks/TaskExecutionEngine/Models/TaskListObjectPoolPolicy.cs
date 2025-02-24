#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Specialized pool policy for List{ITask} with additional optimizations
/// </summary>
internal sealed class TaskListObjectPoolPolicy : PooledObjectPolicy<List<ITask>>
{
    private const int DefaultCapacity = 32;
    private readonly ConditionalWeakTable<List<ITask>, object?> _sizes;

    public TaskListObjectPoolPolicy(ConditionalWeakTable<List<ITask>, object?> sizes)
    {
        _sizes = sizes;
    }

    public override List<ITask> Create()
    {
        return new List<ITask>(DefaultCapacity);
    }

    public override bool Return(List<ITask> obj)
    {
        if (obj == null)
        {
            return false;
        }

        try
        {
            obj.Clear();

            // Resize if necessary based on tracked size
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
