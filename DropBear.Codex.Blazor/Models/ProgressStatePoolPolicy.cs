#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Policy for creating and resetting progress state objects in the pool
/// </summary>
internal sealed class ProgressStatePoolPolicy : IPooledObjectPolicy<ProgressState>
{
    public ProgressState Create()
    {
        return new ProgressState();
    }

    public bool Return(ProgressState obj)
    {
        // Cleanup and reset the state before returning to pool
        try
        {
            obj.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
