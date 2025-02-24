#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Provides simple timing and logging for cache operations to track cache hits and misses.
/// </summary>
public sealed class CacheMetrics
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly Stopwatch _stopwatch;

    /// <summary>
    ///     Initializes a new instance of <see cref="CacheMetrics" />, automatically starting the timer.
    /// </summary>
    /// <param name="operation">A descriptive name for the operation being measured (e.g. "GetOrSet{T}").</param>
    public CacheMetrics(string operation)
    {
        _operation = operation;
        _logger = LoggerFactory.Logger.ForContext<CacheMetrics>();
        _stopwatch = Stopwatch.StartNew(); // *** CHANGE *** Start immediately
    }

    /// <summary>
    ///     Logs a cache hit event, stopping the timer and recording elapsed milliseconds.
    /// </summary>
    /// <param name="key">The key that was found in the cache.</param>
    public void LogCacheHit(string key)
    {
        _stopwatch.Stop();
        _logger.Information(
            "Cache HIT for {Operation}. Key: {Key}. Duration: {Duration}ms",
            _operation,
            key,
            _stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    ///     Logs a cache miss event, stopping the timer and recording elapsed milliseconds.
    /// </summary>
    /// <param name="key">The key that was not found in the cache.</param>
    public void LogCacheMiss(string key)
    {
        _stopwatch.Stop();
        _logger.Information(
            "Cache MISS for {Operation}. Key: {Key}. Duration: {Duration}ms",
            _operation,
            key,
            _stopwatch.ElapsedMilliseconds);
    }
}
