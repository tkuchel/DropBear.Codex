#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service for collecting and managing performance metrics for the DropBearDataGrid component.
/// </summary>
public class DataGridMetricsService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DataGridMetricsService>();
    private readonly Stopwatch _searchTimer = new();
    private readonly Queue<double> _searchTimes = new(10); // Keep last 10 search times

    public bool IsEnabled { get; set; }
    public double LastSearchTime { get; private set; }
    public double AverageSearchTime => _searchTimes.Any() ? _searchTimes.Average() : 0;
    public int TotalItemsProcessed { get; private set; }
    public int FilteredItemCount { get; private set; }
    public int DisplayedItemCount { get; private set; }
    public double ItemsPerSecond => LastSearchTime > 0 ? TotalItemsProcessed / (LastSearchTime / 1000) : 0;

    /// <summary>
    ///     Starts timing a search operation.
    /// </summary>
    public void StartSearchTimer()
    {
        if (!IsEnabled)
        {
            return;
        }

        _searchTimer.Restart();
        Logger.Debug("Search timer started");
    }

    /// <summary>
    ///     Stops timing the current search operation and records metrics.
    /// </summary>
    /// <param name="totalItems">Total number of items processed</param>
    /// <param name="filteredItems">Number of items after filtering</param>
    /// <param name="displayedItems">Number of items currently displayed</param>
    public void StopSearchTimer(int totalItems, int filteredItems, int displayedItems)
    {
        if (!IsEnabled)
        {
            return;
        }

        _searchTimer.Stop();
        LastSearchTime = _searchTimer.Elapsed.TotalMilliseconds;

        // Keep only the last 10 search times
        if (_searchTimes.Count >= 10)
        {
            _searchTimes.Dequeue();
        }

        _searchTimes.Enqueue(LastSearchTime);

        TotalItemsProcessed = totalItems;
        FilteredItemCount = filteredItems;
        DisplayedItemCount = displayedItems;

        Logger.Information(
            "Search completed - Time: {SearchTime}ms, Items: {TotalItems}, Filtered: {FilteredItems}, Displayed: {DisplayedItems}, Items/sec: {ItemsPerSecond}",
            LastSearchTime,
            totalItems,
            filteredItems,
            displayedItems,
            ItemsPerSecond
        );
    }

    /// <summary>
    ///     Resets all metrics to their default values.
    /// </summary>
    public void Reset()
    {
        _searchTimer.Reset();
        _searchTimes.Clear();
        LastSearchTime = 0;
        TotalItemsProcessed = 0;
        FilteredItemCount = 0;
        DisplayedItemCount = 0;

        Logger.Debug("Metrics reset");
    }
}
