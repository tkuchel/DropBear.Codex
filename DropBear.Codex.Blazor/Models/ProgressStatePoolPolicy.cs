#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Defines creation and return policies for <see cref="ProgressState" /> objects in the pool.
/// </summary>
internal sealed class ProgressStatePoolPolicy : IPooledObjectPolicy<ProgressState>
{
    /// <summary>
    ///     Creates a new <see cref="ProgressState" /> instance.
    /// </summary>
    /// <returns>A new <see cref="ProgressState" />.</returns>
    public ProgressState Create()
    {
        return new ProgressState();
    }

    /// <summary>
    ///     Called when returning a <see cref="ProgressState" /> to the pool.
    ///     Disposes the object's resources and returns a boolean indicating whether it can be reused.
    /// </summary>
    /// <param name="obj">The <see cref="ProgressState" /> being returned.</param>
    /// <returns>
    ///     True if the object was successfully reset and can be reused;
    ///     false if it should be dropped from the pool.
    /// </returns>
    public bool Return(ProgressState obj)
    {
        // Cleanup and reset the state before returning to the pool
        try
        {
            // Synchronously disposing the async resource here:
            obj.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            // If something goes wrong, don't return it to the pool
            return false;
        }
    }
}
