namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Defines an interface for pooled result objects that can be reset.
///     Only used by the legacy compatibility layer.
/// </summary>
public interface IPooledResult
{
    /// <summary>
    ///     Resets this object to its initial, default state before re-entering the pool.
    /// </summary>
    void Reset();
}
