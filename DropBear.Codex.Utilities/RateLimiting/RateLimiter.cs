#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.RateLimiting;

/// <summary>
///     Provides thread-safe rate limiting functionality using sliding window algorithm.
/// </summary>
/// <remarks>
///     Useful for protecting encryption caching, API endpoints, and other resources from abuse.
///     Implements a sliding window counter for accurate rate limiting.
/// </remarks>
public sealed class RateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, RequestWindow> _windows;
    private readonly int _maxRequests;
    private readonly TimeSpan _windowDuration;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimiter"/> class.
    /// </summary>
    /// <param name="maxRequests">Maximum number of requests allowed within the window.</param>
    /// <param name="windowDuration">Duration of the sliding window.</param>
    /// <param name="cleanupInterval">Interval for cleaning up expired entries (default: window duration).</param>
    public RateLimiter(int maxRequests, TimeSpan windowDuration, TimeSpan? cleanupInterval = null)
    {
        if (maxRequests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "Max requests must be greater than zero.");
        }

        if (windowDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDuration), "Window duration must be positive.");
        }

        _maxRequests = maxRequests;
        _windowDuration = windowDuration;
        _windows = new ConcurrentDictionary<string, RequestWindow>(StringComparer.Ordinal);

        var cleanup = cleanupInterval ?? windowDuration;
        _cleanupTimer = new Timer(CleanupExpiredWindows, null, cleanup, cleanup);
    }

    /// <summary>
    ///     Gets the maximum number of requests allowed.
    /// </summary>
    public int MaxRequests => _maxRequests;

    /// <summary>
    ///     Gets the window duration.
    /// </summary>
    public TimeSpan WindowDuration => _windowDuration;

    /// <summary>
    ///     Gets the current number of tracked keys.
    /// </summary>
    public int TrackedKeyCount => _windows.Count;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cleanupTimer.Dispose();
        _windows.Clear();
        _disposed = true;
    }

    /// <summary>
    ///     Attempts to acquire permission for a request.
    /// </summary>
    /// <param name="key">The key identifying the client/resource (e.g., user ID, IP address).</param>
    /// <returns>
    ///     A Result containing RateLimitInfo if successful, or an error if rate limit exceeded.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<RateLimitInfo, UtilityError> TryAcquire(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = DateTime.UtcNow;
        var window = _windows.GetOrAdd(key, _ => new RequestWindow(_windowDuration));

        lock (window.Lock)
        {
            // Remove timestamps outside the current window
            window.RemoveExpiredTimestamps(now);

            if (window.RequestCount >= _maxRequests)
            {
                var retryAfter = window.GetEarliestTimestamp()?.Add(_windowDuration) - now ?? TimeSpan.Zero;

                return Result<RateLimitInfo, UtilityError>.Failure(
                    UtilityError.RateLimitExceeded(
                        $"Rate limit of {_maxRequests} requests per {_windowDuration.TotalSeconds}s exceeded for key '{key}'",
                        retryAfter));
            }

            // Add current request timestamp
            window.AddTimestamp(now);

            var info = new RateLimitInfo
            {
                Key = key,
                RequestCount = window.RequestCount,
                RemainingRequests = _maxRequests - window.RequestCount,
                MaxRequests = _maxRequests,
                WindowDuration = _windowDuration,
                ResetTime = now.Add(_windowDuration)
            };

            return Result<RateLimitInfo, UtilityError>.Success(info);
        }
    }

    /// <summary>
    ///     Attempts to acquire permission asynchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<Result<RateLimitInfo, UtilityError>> TryAcquireAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryAcquire(key));
    }

    /// <summary>
    ///     Gets rate limit information for a key without acquiring.
    /// </summary>
    public RateLimitInfo? GetInfo(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_windows.TryGetValue(key, out var window))
        {
            return null;
        }

        lock (window.Lock)
        {
            window.RemoveExpiredTimestamps(DateTime.UtcNow);

            return new RateLimitInfo
            {
                Key = key,
                RequestCount = window.RequestCount,
                RemainingRequests = _maxRequests - window.RequestCount,
                MaxRequests = _maxRequests,
                WindowDuration = _windowDuration,
                ResetTime = DateTime.UtcNow.Add(_windowDuration)
            };
        }
    }

    /// <summary>
    ///     Resets the rate limit for a specific key.
    /// </summary>
    public bool Reset(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _windows.TryRemove(key, out _);
    }

    /// <summary>
    ///     Resets all rate limits.
    /// </summary>
    public void ResetAll()
    {
        _windows.Clear();
    }

    private void CleanupExpiredWindows(object? state)
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var (key, window) in _windows)
        {
            lock (window.Lock)
            {
                window.RemoveExpiredTimestamps(now);

                // Remove windows with no recent requests
                if (window.RequestCount == 0 &&
                    window.GetEarliestTimestamp() is { } earliest &&
                    now - earliest > _windowDuration * 2)
                {
                    expiredKeys.Add(key);
                }
            }
        }

        foreach (var key in expiredKeys)
        {
            _windows.TryRemove(key, out _);
        }
    }

    /// <summary>
    ///     Represents a sliding window of request timestamps for a single key.
    /// </summary>
    private sealed class RequestWindow
    {
        private readonly LinkedList<DateTime> _timestamps;
        private readonly TimeSpan _windowDuration;

        public RequestWindow(TimeSpan windowDuration)
        {
            _windowDuration = windowDuration;
            _timestamps = new LinkedList<DateTime>();
            Lock = new object();
        }

        public object Lock { get; }
        public int RequestCount => _timestamps.Count;

        public void AddTimestamp(DateTime timestamp)
        {
            _timestamps.AddLast(timestamp);
        }

        public DateTime? GetEarliestTimestamp()
        {
            return _timestamps.First?.Value;
        }

        public void RemoveExpiredTimestamps(DateTime now)
        {
            var cutoff = now - _windowDuration;

            while (_timestamps.First is { } node && node.Value < cutoff)
            {
                _timestamps.RemoveFirst();
            }
        }
    }
}
