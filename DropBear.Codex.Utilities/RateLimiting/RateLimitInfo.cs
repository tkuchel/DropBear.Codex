#region

#endregion

namespace DropBear.Codex.Utilities.RateLimiting;

/// <summary>
///     Contains information about the current rate limit status.
/// </summary>
public sealed record RateLimitInfo
{
    /// <summary>
    ///     Gets the key this rate limit applies to.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     Gets the current number of requests in the window.
    /// </summary>
    public required int RequestCount { get; init; }

    /// <summary>
    ///     Gets the number of requests remaining before hitting the limit.
    /// </summary>
    public required int RemainingRequests { get; init; }

    /// <summary>
    ///     Gets the maximum requests allowed.
    /// </summary>
    public required int MaxRequests { get; init; }

    /// <summary>
    ///     Gets the window duration.
    /// </summary>
    public required TimeSpan WindowDuration { get; init; }

    /// <summary>
    ///     Gets the time when the rate limit will reset.
    /// </summary>
    public required DateTime ResetTime { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the limit has been reached.
    /// </summary>
    public bool IsLimitReached => RemainingRequests <= 0;

    /// <summary>
    ///     Gets the percentage of the limit consumed (0-100).
    /// </summary>
    public double PercentageUsed => (RequestCount / (double)MaxRequests) * 100;
}
