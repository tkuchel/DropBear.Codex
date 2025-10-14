namespace DropBear.Codex.Workflow.Configuration;

/// <summary>
///     Retry policy configuration for workflow steps.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    ///     Maximum number of retry attempts.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    ///     Base delay for exponential backoff.
    /// </summary>
    public required TimeSpan BaseDelay { get; init; }

    /// <summary>
    ///     Maximum delay between attempts.
    /// </summary>
    public required TimeSpan MaxDelay { get; init; }

    /// <summary>
    ///     Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    ///     Predicate to determine if an exception should trigger a retry.
    /// </summary>
    public Func<Exception, bool>? ShouldRetryPredicate { get; init; }

    /// <summary>
    ///     Creates a default retry policy.
    /// </summary>
    public static RetryPolicy Default { get; } = new()
    {
        MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(100), MaxDelay = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    ///     Creates a retry policy with no retries.
    /// </summary>
    public static RetryPolicy None { get; } = new()
    {
        MaxAttempts = 0, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero
    };
}
