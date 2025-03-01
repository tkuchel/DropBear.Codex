#region

using System.Diagnostics;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides performance metrics tracking for the DataGrid component.
/// </summary>
public sealed class DataGridMetricsService
{
    private readonly object _lockObject = new();
    private readonly Stopwatch _searchTimer = new();
    private readonly List<double> _searchTimes = new();

    /// <summary>
    ///     Gets the last search operation execution time in milliseconds.
    /// </summary>
    public double LastSearchTime { get; private set; }

    /// <summary>
    ///     Gets the average search operation execution time in milliseconds.
    /// </summary>
    public double AverageSearchTime { get; private set; }

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
    ///     Gets the number of items displayed in the grid (pagination affects this).
    /// </summary>
    public int DisplayedItemCount { get; private set; }

    /// <summary>
    ///     Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Gets the number of search operations tracked.
    /// </summary>
    public int OperationCount => _searchTimes.Count;

    /// <summary>
    ///     Starts the search timer.
    /// </summary>
    public void StartSearchTimer()
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_lockObject)
        {
            _searchTimer.Reset();
            _searchTimer.Start();
        }
    }

    /// <summary>
    ///     Stops the search timer and records metrics.
    /// </summary>
    /// <param name="totalItems">The total number of items processed.</param>
    /// <param name="filteredItems">The number of items after filtering.</param>
    /// <param name="displayedItems">The number of items displayed after pagination.</param>
    public void StopSearchTimer(int totalItems, int filteredItems, int displayedItems)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_lockObject)
        {
            _searchTimer.Stop();

            var elapsed = _searchTimer.Elapsed.TotalMilliseconds;
            LastSearchTime = elapsed;
            _searchTimes.Add(elapsed);

            // Keep only the last 100 measurements for average
            if (_searchTimes.Count > 100)
            {
                _searchTimes.RemoveAt(0);
            }

            AverageSearchTime = _searchTimes.Average();

            TotalItemsProcessed = totalItems;
            FilteredItemCount = filteredItems;
            DisplayedItemCount = displayedItems;

            // Calculate items per second if the operation took time
            if (elapsed > 0)
            {
                ItemsPerSecond = totalItems / elapsed * 1000;
            }
            else
            {
                ItemsPerSecond = totalItems; // Instant operation
            }
        }
    }

    /// <summary>
    ///     Resets all metrics tracking.
    /// </summary>
    public void Reset()
    {
        lock (_lockObject)
        {
            _searchTimer.Reset();
            _searchTimes.Clear();
            LastSearchTime = 0;
            AverageSearchTime = 0;
            ItemsPerSecond = 0;
            TotalItemsProcessed = 0;
            FilteredItemCount = 0;
            DisplayedItemCount = 0;
        }
    }

    /// <summary>
    ///     Gets metrics data for the last search operation.
    /// </summary>
    /// <returns>A dictionary of metric names and values.</returns>
    public IDictionary<string, object> GetMetricsData()
    {
        var metrics = new Dictionary<string, object>();

        lock (_lockObject)
        {
            metrics["LastSearchTime"] = LastSearchTime;
            metrics["AverageSearchTime"] = AverageSearchTime;
            metrics["ItemsPerSecond"] = ItemsPerSecond;
            metrics["TotalItemsProcessed"] = TotalItemsProcessed;
            metrics["FilteredItemCount"] = FilteredItemCount;
            metrics["DisplayedItemCount"] = DisplayedItemCount;
            metrics["OperationCount"] = OperationCount;
        }

        return metrics;
    }
}
