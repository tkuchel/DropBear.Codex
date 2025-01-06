#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Provides a thread-safe pool of <see cref="ProgressState" /> objects,
///     reducing allocation overhead when creating many short-lived progress states.
/// </summary>
internal sealed class ProgressStatePool
{
    private static readonly DefaultObjectPoolProvider PoolProvider
        = new() { MaximumRetained = 20 };

    private readonly ObjectPool<ProgressState> _pool;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressStatePool" /> class
    ///     using a custom <see cref="ProgressStatePoolPolicy" />.
    /// </summary>
    public ProgressStatePool()
    {
        var policy = new ProgressStatePoolPolicy();
        _pool = PoolProvider.Create(policy);
    }

    /// <summary>
    ///     Retrieves a <see cref="ProgressState" /> object from the pool.
    ///     Call <see cref="Return" /> when finished.
    /// </summary>
    /// <returns>A pooled <see cref="ProgressState" /> instance.</returns>
    public ProgressState Get()
    {
        return _pool.Get();
    }

    /// <summary>
    ///     Returns a <see cref="ProgressState" /> to the pool,
    ///     allowing it to be reused for future requests.
    /// </summary>
    /// <param name="state">The <see cref="ProgressState" /> instance to return.</param>
    public void Return(ProgressState state)
    {
        _pool.Return(state);
    }
}
