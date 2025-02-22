#region

#endregion

namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Wrapper for configuring async enumerable behavior.
/// </summary>
internal sealed class ConfiguredAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly bool _continueOnCapturedContext;
    private readonly IAsyncEnumerable<T> _source;

    public ConfiguredAsyncEnumerable(IAsyncEnumerable<T> source, bool continueOnCapturedContext)
    {
        _source = source;
        _continueOnCapturedContext = continueOnCapturedContext;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new ConfiguredAsyncEnumerator<T>(_source.GetAsyncEnumerator(cancellationToken),
            _continueOnCapturedContext);
    }
}
