#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.Caching;

public class CacheMetrics
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly Stopwatch _stopwatch;

    public CacheMetrics(string operation)
    {
        _operation = operation;
        _logger = LoggerFactory.Logger.ForContext<CacheMetrics>();
        _stopwatch = new Stopwatch();
        _stopwatch.Start();
    }

    public void LogCacheHit(string key)
    {
        _stopwatch.Stop();
        _logger.Information(
            "Cache HIT for {Operation}. Key: {Key}. Duration: {Duration}ms",
            _operation,
            key,
            _stopwatch.ElapsedMilliseconds);
    }

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
