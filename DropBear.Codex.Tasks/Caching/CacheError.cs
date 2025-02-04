﻿#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Tasks.Caching;

/// <summary>
/// Represents errors that occur during cache operations
/// </summary>
public record CacheError : ResultError
{
    /// <summary>
    /// Initializes a new instance of CacheError
    /// </summary>
    /// <param name="message">The error message</param>
    public CacheError(string message) : base(message) { }

    /// <summary>
    /// Creates an error for when a cache entry is not found
    /// </summary>
    /// <param name="key">The cache key that wasn't found</param>
    public static CacheError NotFound(string key) =>
        new($"Cache entry not found for key: {key}");

    /// <summary>
    /// Creates an error for when a cache operation fails
    /// </summary>
    /// <param name="message">Details about the failure</param>
    public static CacheError OperationFailed(string message) =>
        new($"Cache operation failed: {message}");
}
