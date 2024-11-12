#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Retry;

public sealed class RetryPolicy
{
    private RetryPolicy(
        int maxAttempts,
        Func<int, TimeSpan> delayStrategy,
        Func<Exception, ResultError> errorMapper)
    {
        MaxAttempts = maxAttempts;
        DelayStrategy = delayStrategy;
        ErrorMapper = errorMapper;
    }

    public int MaxAttempts { get; }
    public Func<int, TimeSpan> DelayStrategy { get; }
    public Func<Exception, ResultError> ErrorMapper { get; }

    public static RetryPolicy Create(
        int maxAttempts,
        Func<int, TimeSpan> delayStrategy,
        Func<Exception, ResultError> errorMapper)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        return new RetryPolicy(
            maxAttempts,
            delayStrategy ?? throw new ArgumentNullException(nameof(delayStrategy)),
            errorMapper ?? throw new ArgumentNullException(nameof(errorMapper)));
    }
}
