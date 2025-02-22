namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Wrapper for configuring async enumerator behavior.
/// </summary>
internal sealed class ConfiguredAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly bool _continueOnCapturedContext;
    private readonly IAsyncEnumerator<T> _enumerator;

    public ConfiguredAsyncEnumerator(IAsyncEnumerator<T> enumerator, bool continueOnCapturedContext)
    {
        _enumerator = enumerator;
        _continueOnCapturedContext = continueOnCapturedContext;
    }

    public T Current => _enumerator.Current;

    public async ValueTask<bool> MoveNextAsync()
    {
        return await _enumerator.MoveNextAsync().ConfigureAwait(_continueOnCapturedContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _enumerator.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);
    }
}
