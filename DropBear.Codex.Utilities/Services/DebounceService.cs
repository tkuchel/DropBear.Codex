#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Utilities.Errors;
using DropBear.Codex.Utilities.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Services;

/// <summary>
///     Service for managing debounced function or action calls.
///     Debouncing prevents a method from being called too frequently.
/// </summary>
public class DebounceService : IDebounceService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DebounceService>();

    // Cache options for debounce entries
    private readonly MemoryCacheEntryOptions _defaultCacheOptions;

    private readonly TimeSpan _defaultDebounceTime = TimeSpan.FromSeconds(30);
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DebounceService" /> class.
    ///     Uses an <see cref="IMemoryCache" /> to store timestamps for debouncing function or action calls.
    /// </summary>
    /// <param name="memoryCache">The memory cache used to store timestamps for debouncing.</param>
    public DebounceService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

        // Set up default cache options with sliding expiration
        _defaultCacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(1), Priority = CacheItemPriority.Low
        };
    }

    /// <summary>
    ///     For backwards compatibility with the original implementation.
    /// </summary>
    public async Task<Core.Results.Compatibility.Result<T>> DebounceAsyncLegacy<T>(
        Func<Task<Core.Results.Compatibility.Result<T>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Logger.Warning("Using legacy Result type for debounce operation");

        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            return Core.Results.Compatibility.Result<T>.Failure("Operation debounced.");
        }

        try
        {
            return await function().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during legacy debounced function execution: {Key}", key);
            return Core.Results.Compatibility.Result<T>.Failure(
                "An error occurred while executing the function.", ex);
        }
    }

    /// <summary>
    ///     For backwards compatibility with the original implementation.
    /// </summary>
    public async Task<Result> DebounceAsyncLegacy(
        Action action,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Logger.Warning("Using legacy Result type for debounce operation");

        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            return Result.Failure("Operation debounced.");
        }

        try
        {
            await Task.Run(action).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during legacy debounced action execution: {Key}", key);
            return Result.Failure(
                "An error occurred while executing the action.", ex);
        }
    }

    /// <summary>
    ///     For backwards compatibility with the original implementation.
    /// </summary>
    public async Task<Result> DebounceAsyncLegacy(
        Func<Task<Result>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Logger.Warning("Using legacy Result type for debounce operation");

        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            return Result.Failure("Operation debounced.");
        }

        try
        {
            return await function().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during legacy debounced function execution: {Key}", key);
            return Result.Failure(
                "An error occurred while executing the function.", ex);
        }
    }

    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{T, TError}" />.
    ///     Ensures that the function is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of error that can occur.</typeparam>
    /// <param name="function">The function to execute which returns a <see cref="Result{T, TError}" />.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>A Result containing the function result or a debounce error.</returns>
    public async Task<Result<T, TError>> DebounceAsync<T, TError>(
        Func<Task<Result<T, TError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        where TError : ResultError
    {
        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            Logger.Debug("Operation debounced: {Key}, Time: {DebounceTime}", key, debounceTime);
            return Result<T, TError>.Failure((TError)CreateError(typeof(TError), "Operation debounced."));
        }

        try
        {
            var result = await function().ConfigureAwait(false);
            Logger.Debug("Debounced function executed: {Key}, Success: {Success}", key, result.IsSuccess);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during debounced function execution: {Key}", key);
            return Result<T, TError>.Failure(
                (TError)CreateError(typeof(TError), $"An error occurred while executing the function: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{T, DebounceError}" />.
    ///     Ensures that the function is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="function">The function to execute which returns a <see cref="Result{T, DebounceError}" />.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>A Result containing the function result or a debounce error.</returns>
    public async Task<Result<T, DebounceError>> DebounceAsync<T>(
        Func<Task<Result<T, DebounceError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            Logger.Debug("Operation debounced: {Key}, Time: {DebounceTime}", key, debounceTime);
            return Result<T, DebounceError>.Failure(new DebounceError("Operation debounced."));
        }

        try
        {
            var result = await function().ConfigureAwait(false);
            Logger.Debug("Debounced function executed: {Key}, Success: {Success}", key, result.IsSuccess);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during debounced function execution: {Key}", key);
            return Result<T, DebounceError>.Failure(
                new DebounceError($"An error occurred while executing the function: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Debounces an action that does not return a value.
    ///     Ensures that the action is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="key">An optional unique identifier for the action call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>A Result indicating success or a debounce error.</returns>
    public async Task<Result<Unit, DebounceError>> DebounceAsync(
        Action action,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            Logger.Debug("Operation debounced: {Key}, Time: {DebounceTime}", key, debounceTime);
            return Result<Unit, DebounceError>.Failure(new DebounceError("Operation debounced."));
        }

        try
        {
            await Task.Run(action).ConfigureAwait(false);
            Logger.Debug("Debounced action executed: {Key}", key);
            return Result<Unit, DebounceError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during debounced action execution: {Key}", key);
            return Result<Unit, DebounceError>.Failure(
                new DebounceError($"An error occurred while executing the action: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Debounces a function that returns a <see cref="Result{Unit, DebounceError}" />.
    ///     Ensures that the function is not executed more frequently than the specified minimum interval.
    /// </summary>
    /// <param name="function">The asynchronous function to execute.</param>
    /// <param name="key">An optional unique identifier for the function call used for debouncing purposes.</param>
    /// <param name="debounceTime">The minimum time interval between successive executions.</param>
    /// <param name="caller">Automatically filled by the compiler to provide the method name of the caller.</param>
    /// <param name="filePath">Automatically filled by the compiler to provide the source file path of the caller.</param>
    /// <param name="lineNumber">
    ///     Automatically filled by the compiler to provide the line number in the source code of the
    ///     caller.
    /// </param>
    /// <returns>A Result indicating success or a debounce error.</returns>
    public async Task<Result<Unit, DebounceError>> DebounceAsync(
        Func<Task<Result<Unit, DebounceError>>> function,
        string key = "",
        TimeSpan? debounceTime = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        key = GenerateKey(key, caller, filePath, lineNumber);
        debounceTime ??= _defaultDebounceTime;

        if (IsDebounced(key, debounceTime.Value, out var isFirstCall) && !isFirstCall)
        {
            Logger.Debug("Operation debounced: {Key}, Time: {DebounceTime}", key, debounceTime);
            return Result<Unit, DebounceError>.Failure(new DebounceError("Operation debounced."));
        }

        try
        {
            var result = await function().ConfigureAwait(false);
            Logger.Debug("Debounced function executed: {Key}, Success: {Success}", key, result.IsSuccess);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during debounced function execution: {Key}", key);
            return Result<Unit, DebounceError>.Failure(
                new DebounceError($"An error occurred while executing the function: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Clears all debounce entries from the cache.
    /// </summary>
    public void ClearCache()
    {
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
            Logger.Information("Debounce cache cleared");
        }
    }

    /// <summary>
    ///     Checks if a given key has been debounced within the specified timeframe and identifies if it's the first call.
    /// </summary>
    /// <param name="key">The unique key for the operation to check.</param>
    /// <param name="debounceTime">The debounce interval.</param>
    /// <param name="isFirstCall">Out parameter indicating whether this is the first call.</param>
    /// <returns>True if the operation is still within the debounce time, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsDebounced(string key, TimeSpan debounceTime, out bool isFirstCall)
    {
        var cacheKey = $"Debounce-{key}";
        var lastExecuted = _memoryCache.Get<DateTimeOffset?>(cacheKey);

        if (!lastExecuted.HasValue)
        {
            isFirstCall = true;
            _memoryCache.Set(cacheKey, DateTimeOffset.UtcNow,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = debounceTime, Priority = CacheItemPriority.Low
                });
            return false;
        }

        isFirstCall = false;
        if (DateTimeOffset.UtcNow - lastExecuted.Value < debounceTime)
        {
            return true;
        }

        _memoryCache.Set(cacheKey, DateTimeOffset.UtcNow,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = debounceTime, Priority = CacheItemPriority.Low
            });
        return false;
    }

    /// <summary>
    ///     Generates a unique key for debouncing purposes based on the caller's method, file path, and line number if no key
    ///     is provided.
    /// </summary>
    /// <param name="key">The provided key or an empty string if none is provided.</param>
    /// <param name="caller">The method name of the caller.</param>
    /// <param name="filePath">The file path of the caller.</param>
    /// <param name="lineNumber">The line number of the caller's call in the source code.</param>
    /// <returns>A unique key generated or modified based on the input parameters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateKey(string key, string caller, string filePath, int lineNumber)
    {
        return string.IsNullOrEmpty(key) ? $"{caller}-{Path.GetFileName(filePath)}-{lineNumber}" : key;
    }

    /// <summary>
    ///     Creates an error of the specified type with the given message.
    /// </summary>
    /// <param name="errorType">The type of error to create.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new instance of the specified error type.</returns>
    private static ResultError CreateError(Type errorType, string message)
    {
        return (ResultError)Activator.CreateInstance(errorType, message)!;
    }
}
