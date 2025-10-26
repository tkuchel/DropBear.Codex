#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Cache;

/// <summary>
///     Caches validation results for tasks to avoid repeating expensive checks.
///     Periodically cleans up expired entries and enforces a maximum cache size.
/// </summary>
public sealed class ValidationCache : IDisposable
{
    private const int DefaultMaxSize = 10000;
    private const int CleanupThreshold = 1000;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly SemaphoreSlim _cleanupLock;
    private readonly Task _cleanupTask;
    private readonly PeriodicTimer _cleanupTimer;
    private readonly TimeSpan _defaultExpiration;
    private readonly ILogger _logger;
    private readonly int _maxCacheSize;
    private volatile bool _disposed;

    /// <summary>
    ///     Creates a new instance of <see cref="ValidationCache" />.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <param name="defaultExpiration">Default expiration time for cached validations.</param>
    /// <param name="maxCacheSize">Max number of entries allowed.</param>
    public ValidationCache(
        ILogger logger,
        TimeSpan? defaultExpiration = null,
        int? maxCacheSize = null)
    {
        _logger = logger;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
        _maxCacheSize = maxCacheSize ?? DefaultMaxSize;
        _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
        _cleanupLock = new SemaphoreSlim(1, 1);

        _cleanupTimer = new PeriodicTimer(CleanupInterval);
        _cleanupTask = RunCleanupAsync(); // starts in background
    }

    /// <summary>
    ///     Disposes the cache, stopping cleanup and removing all entries.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer.Dispose();
        _cleanupLock.Dispose();

        _cache.Clear();
    }

    /// <summary>
    ///     Validates a task, using cached results if available and not expired.
    ///     Otherwise, performs validation and stores the result in the cache.
    /// </summary>
    public async Task<Result<Unit, TaskExecutionError>> ValidateTaskAsync(
        ITask task,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        var key = GenerateCacheKey(task);
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            return entry.ValidationResult;
        }

        // Enforce cache size limit
        await EnsureCacheSizeAsync().ConfigureAwait(false);

        try
        {
            var result = await ValidateTaskInternalAsync(task, scope, cancellationToken).ConfigureAwait(false);

            var cacheEntry = new CacheEntry(
                result,
                DateTime.UtcNow.Add(DetermineExpirationTime(task)),
                task.GetType().Name);

            _cache.AddOrUpdate(key, cacheEntry, (_, _) => cacheEntry);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Validation failed for task {TaskName}", task.Name);
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError($"Validation failed for task {task.Name}", task.Name, ex));
        }
    }

    // *** CHANGE ***  We keep the rest the same, just refine comments and inlining.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateCacheKey(ITask task)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // Add task name
        hasher.AppendData(Encoding.UTF8.GetBytes(task.Name));

        // Add dependencies
        foreach (var dep in task.Dependencies.OrderBy(d => d, StringComparer.Ordinal))
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(dep));
        }

        // Add metadata affecting validation (keys starting with "Validate")
        var validationMetadata = task.Metadata
            .Where(m => m.Key.StartsWith("Validate", StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Key, StringComparer.Ordinal);

        foreach (var meta in validationMetadata)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(meta.Key));
            hasher.AppendData(Encoding.UTF8.GetBytes(meta.Value?.ToString() ?? string.Empty));
        }

        return Convert.ToHexString(hasher.GetCurrentHash());
    }

    private async Task<Result<Unit, TaskExecutionError>> ValidateTaskInternalAsync(
        ITask task,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        // Synchronous check
        var validationResult = task.Validate();
        if (!validationResult.IsSuccess)
        {
            // TaskValidationError extends TaskExecutionError, so we can use it directly
            return Result<Unit, TaskExecutionError>.Failure(validationResult.Error);
        }

        // Async check
        if (task is IAsyncValidatable asyncValidatable)
        {
            try
            {
                var isValid = await asyncValidatable.ValidateAsync()
                    .WaitAsync(task.Timeout, cancellationToken)
                    .ConfigureAwait(false);

                if (!isValid)
                {
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError($"Async validation failed for task {task.Name}", task.Name));
                }
            }
            catch (TimeoutException)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError($"Async validation timed out for task {task.Name}", task.Name));
            }
            catch (Exception ex)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError($"Async validation threw exception for task {task.Name}", task.Name, ex));
            }
        }

        // Condition check
        if (task.Condition != null && !task.Condition(scope.Context))
        {
            return Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                new TaskExecutionError($"Task {task.Name} skipped due to condition", task.Name));
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Ensures the cache does not exceed its maximum size, removing old entries if necessary.
    /// </summary>
    private async Task EnsureCacheSizeAsync()
    {
        if (_cache.Count >= _maxCacheSize - CleanupThreshold)
        {
            await _cleanupLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cache.Count >= _maxCacheSize - CleanupThreshold)
                {
                    RemoveExpiredEntries();
                    if (_cache.Count >= _maxCacheSize - CleanupThreshold)
                    {
                        RemoveOldestEntries(_maxCacheSize / 10);
                    }
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }
    }

    /// <summary>
    ///     Periodically removes expired entries from the cache.
    /// </summary>
    private async Task RunCleanupAsync()
    {
        try
        {
            while (await _cleanupTimer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                if (_disposed)
                {
                    break;
                }

                await _cleanupLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    RemoveExpiredEntries();
                }
                finally
                {
                    _cleanupLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan DetermineExpirationTime(ITask task)
    {
        // Tasks with conditions might need more frequent validation
        if (task.Condition != null)
        {
            return TimeSpan.FromMinutes(5);
        }

        // Tasks with dependencies might need moderately frequent validation
        if (task.Dependencies.Any())
        {
            return TimeSpan.FromMinutes(15);
        }

        return _defaultExpiration;
    }

    private void RemoveExpiredEntries()
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.Debug("Removed {Count} expired validation cache entries", expiredKeys.Count);
        }
    }

    private void RemoveOldestEntries(int count)
    {
        var oldestEntries = _cache
            .OrderBy(kvp => kvp.Value.CreatedAt)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestEntries)
        {
            _cache.TryRemove(key, out _);
        }

        _logger.Debug("Removed {Count} oldest validation cache entries", oldestEntries.Count);
    }

    private sealed class CacheEntry
    {
        public CacheEntry(
            Result<Unit, TaskExecutionError> validationResult,
            DateTime expiresAt,
            string taskType)
        {
            ValidationResult = validationResult;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            TaskType = taskType;
        }

        public Result<Unit, TaskExecutionError> ValidationResult { get; }
        public DateTime ExpiresAt { get; }
        public DateTime CreatedAt { get; }
        public string TaskType { get; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
