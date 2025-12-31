using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Extensions;
using DropBear.Codex.Blazor.Models;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service for managing progress operations with optimizations for Blazor Server.
/// </summary>
public sealed class ProgressService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ProgressOperation> _operations = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<ProgressService> _logger;
    private bool _disposed;

    public ProgressService(ILogger<ProgressService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cleanupTimer = new Timer(CleanupExpiredOperations, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     Creates a new progress operation.
    /// </summary>
    /// <param name="operationId">Unique identifier for the operation.</param>
    /// <param name="steps">Optional steps for the operation.</param>
    /// <returns>A progress operation instance.</returns>
    public ProgressOperation CreateOperation(string operationId, IReadOnlyList<ProgressStepConfig>? steps = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProgressService));
        }

        var operation = new ProgressOperation(operationId, steps, _logger);
        _operations[operationId] = operation;

        return operation;
    }

    /// <summary>
    ///     Gets an existing progress operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The progress operation, or null if not found.</returns>
    public ProgressOperation? GetOperation(string operationId)
    {
        return _operations.TryGetValue(operationId, out var operation) ? operation : null;
    }

    /// <summary>
    ///     Removes a progress operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>True if the operation was removed.</returns>
    public bool RemoveOperation(string operationId)
    {
        if (_operations.TryRemove(operationId, out var operation))
        {
            operation.Dispose();
            return true;
        }

        return false;
    }

    private void CleanupExpiredOperations(object? state)
    {
        if (_disposed) return;

        var expiredOperations = _operations
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var operationId in expiredOperations)
        {
            RemoveOperation(operationId);
        }

        if (expiredOperations.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired progress operations", expiredOperations.Count);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        await _cleanupTimer.DisposeAsync().ConfigureAwait(false);

        foreach (var operation in _operations.Values)
        {
            operation.Dispose();
        }

        _operations.Clear();
    }
}

