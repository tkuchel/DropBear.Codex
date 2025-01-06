#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Retry;

/// <summary>
///     Represents a policy for retrying operations, including maximum attempts, delay strategy, and error mapping.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RetryPolicy" /> class.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts before giving up.</param>
    /// <param name="delayStrategy">
    ///     A function taking the current attempt (zero-based) and returning a <see cref="TimeSpan" />
    ///     representing the delay before the next attempt.
    /// </param>
    /// <param name="errorMapper">A function mapping an <see cref="Exception" /> to a <see cref="ResultError" />.</param>
    private RetryPolicy(
        int maxAttempts,
        Func<int, TimeSpan> delayStrategy,
        Func<Exception, ResultError> errorMapper)
    {
        MaxAttempts = maxAttempts;
        DelayStrategy = delayStrategy;
        ErrorMapper = errorMapper;
    }

    /// <summary>
    ///     Gets the maximum number of attempts.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    ///     Gets the function that provides a delay <see cref="TimeSpan" /> based on the current attempt index.
    /// </summary>
    public Func<int, TimeSpan> DelayStrategy { get; }

    /// <summary>
    ///     Gets the function mapping an <see cref="Exception" /> to a <see cref="ResultError" />.
    /// </summary>
    public Func<Exception, ResultError> ErrorMapper { get; }

    /// <summary>
    ///     Creates a new <see cref="RetryPolicy" /> with the specified parameters.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts.</param>
    /// <param name="delayStrategy">A function returning the delay before each attempt.</param>
    /// <param name="errorMapper">A function mapping exceptions to <see cref="ResultError" />.</param>
    /// <returns>A new <see cref="RetryPolicy" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxAttempts" /> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="delayStrategy" /> or <paramref name="errorMapper" /> is null.
    /// </exception>
    public static RetryPolicy Create(
        int maxAttempts,
        Func<int, TimeSpan> delayStrategy,
        Func<Exception, ResultError> errorMapper)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be greater than 0.");
        }

        return new RetryPolicy(
            maxAttempts,
            delayStrategy ?? throw new ArgumentNullException(nameof(delayStrategy)),
            errorMapper ?? throw new ArgumentNullException(nameof(errorMapper)));
    }
}
