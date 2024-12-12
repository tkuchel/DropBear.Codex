#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Provides a thread-safe pool of progress state objects
/// </summary>
internal sealed class ProgressStatePool
{
    private static readonly DefaultObjectPoolProvider _poolProvider = new() { MaximumRetained = 20 };
    private readonly ObjectPool<ProgressState> _pool;

    public ProgressStatePool()
    {
        var policy = new ProgressStatePoolPolicy();
        _pool = _poolProvider.Create(policy);
    }

    /// <summary>
    ///     Gets a progress state from the pool
    /// </summary>
    public ProgressState Get()
    {
        return _pool.Get();
    }

    /// <summary>
    ///     Returns a progress state to the pool
    /// </summary>
    public void Return(ProgressState state)
    {
        _pool.Return(state);
    }
}
