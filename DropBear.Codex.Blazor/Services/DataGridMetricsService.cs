#region

using System.Diagnostics;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides thread-safe performance metrics tracking for the DataGrid component with optimized
///     memory usage and enhanced error handling.
/// </summary>
public sealed class DataGridMetricsService
{
    #region Fields and Constants

    /// <summary>
    ///     Maximum number of search time measurements to keep for calculating averages.
    /// </summary>
    private const int MaxHistorySize = 100;

    /// <summary>
    ///     Thread synchronization lock for metrics operations.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    ///     High-precision timer for measuring search operations.
    /// </summary>
    private readonly Stopwatch _searchTimer = new();

    /// <summary>
    ///     Circular buffer for storing search times, more memory efficient than a growing list.
    /// </summary>
    private readonly double[] _searchTimes = new double[MaxHistorySize];

    /// <summary>
    ///     Current position in the circular buffer.
    /// </summary>
    private int _searchTimesIndex;

    /// <summary>
    ///     Number of collected search time measurements.
    /// </summary>
    private int _searchTimesCount;

    /// <summary>
    ///     Indicates whether this service has been disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets the last search operation execution time in milliseconds.
    /// </summary>
    public double LastSearchTime { get; private set; }

    /// <summary>
    ///     Gets the average search operation execution time in milliseconds,
    ///     calculated from the most recent measurements.
    /// </summary>
    public double AverageSearchTime => GetAverageSearchTime();

    /// <summary>
    ///     Gets the number of items processed per second during the last search operation.
    /// </summary>
    public double ItemsPerSecond { get; private set; }

    /// <summary>
    ///     Gets the total number of items processed in the last operation.
    /// </summary>
    public int TotalItemsProcessed { get; private set; }

    /// <summary>
    ///     Gets the number of items that matched the filter criteria.
    /// </summary>
    public int FilteredItemCount { get; private set; }

    /// <summary>
    ///     Gets the number of items displayed in the grid (after pagination).
    /// </summary>
    public int DisplayedItemCount { get; private set; }

    /// <summary>
    ///     Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Gets the number of search operations tracked.
    /// </summary>
    public int OperationCount => _searchTimesCount;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Starts the search timer to measure the duration of a search operation.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, DataGridError> StartSearchTimer()
    {
        if (!IsEnabled)
        {
            return Result<Unit, DataGridError>.Success(Unit.Value);
        }

        try
        {
#pragma warning disable CA1416 // Blazor Server context - browser warning not applicable
            _lock.Wait();
#pragma warning restore CA1416
            try
            {
                _searchTimer.Reset();
                _searchTimer.Start();
                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to start search timer: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Stops the search timer and records metrics.
    /// </summary>
    /// <param name="totalItems">The total number of items processed.</param>
    /// <param name="filteredItems">The number of items after filtering.</param>
    /// <param name="displayedItems">The number of items displayed after pagination.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, DataGridError> StopSearchTimer(int totalItems, int filteredItems, int displayedItems)
    {
        if (!IsEnabled)
        {
            return Result<Unit, DataGridError>.Success(Unit.Value);
        }

        try
        {
#pragma warning disable CA1416 // Blazor Server context - browser warning not applicable
            _lock.Wait();
#pragma warning restore CA1416
            try
            {
                _searchTimer.Stop();

                var elapsed = _searchTimer.Elapsed.TotalMilliseconds;
                LastSearchTime = elapsed;

                // Add to circular buffer
                AddSearchTime(elapsed);

                TotalItemsProcessed = totalItems;
                FilteredItemCount = filteredItems;
                DisplayedItemCount = displayedItems;

                // Calculate items per second
                if (elapsed > 0)
                {
                    ItemsPerSecond = totalItems / elapsed * 1000;
                }
                else
                {
                    ItemsPerSecond = totalItems; // Instant operation
                }

                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to stop search timer: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously starts the search timer.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, DataGridError>> StartSearchTimerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Result<Unit, DataGridError>.Success(Unit.Value);
        }

        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _searchTimer.Reset();
                _searchTimer.Start();
                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed("Search timer operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to start search timer: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously stops the search timer and records metrics.
    /// </summary>
    /// <param name="totalItems">The total number of items processed.</param>
    /// <param name="filteredItems">The number of items after filtering.</param>
    /// <param name="displayedItems">The number of items displayed after pagination.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, DataGridError>> StopSearchTimerAsync(
        int totalItems,
        int filteredItems,
        int displayedItems,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Result<Unit, DataGridError>.Success(Unit.Value);
        }

        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _searchTimer.Stop();

                var elapsed = _searchTimer.Elapsed.TotalMilliseconds;
                LastSearchTime = elapsed;

                // Add to circular buffer
                AddSearchTime(elapsed);

                TotalItemsProcessed = totalItems;
                FilteredItemCount = filteredItems;
                DisplayedItemCount = displayedItems;

                // Calculate items per second
                if (elapsed > 0)
                {
                    ItemsPerSecond = totalItems / elapsed * 1000;
                }
                else
                {
                    ItemsPerSecond = totalItems; // Instant operation
                }

                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed("Search timer operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to stop search timer: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Resets all metrics tracking.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    public Result<Unit, DataGridError> Reset()
    {
        try
        {
#pragma warning disable CA1416 // Blazor Server context - browser warning not applicable
            _lock.Wait();
#pragma warning restore CA1416
            try
            {
                _searchTimer.Reset();
                ResetSearchTimes();
                LastSearchTime = 0;
                ItemsPerSecond = 0;
                TotalItemsProcessed = 0;
                FilteredItemCount = 0;
                DisplayedItemCount = 0;

                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to reset metrics: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously resets all metrics tracking.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result.</returns>
    public async Task<Result<Unit, DataGridError>> ResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _searchTimer.Reset();
                ResetSearchTimes();
                LastSearchTime = 0;
                ItemsPerSecond = 0;
                TotalItemsProcessed = 0;
                FilteredItemCount = 0;
                DisplayedItemCount = 0;

                return Result<Unit, DataGridError>.Success(Unit.Value);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed("Reset operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<Unit, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to reset metrics: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Gets metrics data for the last search operation.
    /// </summary>
    /// <returns>A Result containing a dictionary of metric names and values.</returns>
    public Result<IDictionary<string, object>, DataGridError> GetMetricsData()
    {
        try
        {
#pragma warning disable CA1416 // Blazor Server context - browser warning not applicable
            _lock.Wait();
#pragma warning restore CA1416
            try
            {
                var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["LastSearchTime"] = LastSearchTime,
                    ["AverageSearchTime"] = GetAverageSearchTime(),
                    ["ItemsPerSecond"] = ItemsPerSecond,
                    ["TotalItemsProcessed"] = TotalItemsProcessed,
                    ["FilteredItemCount"] = FilteredItemCount,
                    ["DisplayedItemCount"] = DisplayedItemCount,
                    ["OperationCount"] = OperationCount
                };

                return Result<IDictionary<string, object>, DataGridError>.Success(metrics);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            return Result<IDictionary<string, object>, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to get metrics data: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously gets metrics data for the last search operation.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result containing metrics.</returns>
    public async Task<Result<IDictionary<string, object>, DataGridError>> GetMetricsDataAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["LastSearchTime"] = LastSearchTime,
                    ["AverageSearchTime"] = GetAverageSearchTime(),
                    ["ItemsPerSecond"] = ItemsPerSecond,
                    ["TotalItemsProcessed"] = TotalItemsProcessed,
                    ["FilteredItemCount"] = FilteredItemCount,
                    ["DisplayedItemCount"] = DisplayedItemCount,
                    ["OperationCount"] = OperationCount
                };

                return Result<IDictionary<string, object>, DataGridError>.Success(metrics);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<IDictionary<string, object>, DataGridError>.Failure(
                DataGridError.OperationFailed("GetMetricsData operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<IDictionary<string, object>, DataGridError>.Failure(
                DataGridError.OperationFailed($"Failed to get metrics data: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Disposes the resources used by this service.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lock.Dispose();
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Adds a search time measurement to the circular buffer.
    /// </summary>
    /// <param name="time">The search time to add, in milliseconds.</param>
    private void AddSearchTime(double time)
    {
        _searchTimes[_searchTimesIndex] = time;
        _searchTimesIndex = (_searchTimesIndex + 1) % _searchTimes.Length;
        _searchTimesCount = Math.Min(_searchTimesCount + 1, _searchTimes.Length);
    }

    /// <summary>
    ///     Calculates the average search time from the collected measurements.
    /// </summary>
    /// <returns>The average search time in milliseconds.</returns>
    private double GetAverageSearchTime()
    {
        if (_searchTimesCount == 0)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < _searchTimesCount; i++)
        {
            sum += _searchTimes[i];
        }

        return sum / _searchTimesCount;
    }

    /// <summary>
    ///     Resets the search times buffer.
    /// </summary>
    private void ResetSearchTimes()
    {
        Array.Clear(_searchTimes, 0, _searchTimes.Length);
        _searchTimesIndex = 0;
        _searchTimesCount = 0;
    }

    #endregion
}
