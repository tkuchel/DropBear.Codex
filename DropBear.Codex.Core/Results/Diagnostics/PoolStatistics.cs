namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Tracks statistics for telemetry operations.
///     Reused from ObjectPoolManager for consistency.
/// </summary>
public sealed class PoolStatistics
{
    private long _totalGets;
    private long _totalReturns;

    /// <summary>
    ///   Initializes a new instance of the <see cref="PoolStatistics" /> class.
    /// </summary>
    /// <param name="typeName">A string representing the type name.</param>
    public PoolStatistics(string typeName)
    {
        TypeName = typeName;
    }

    /// <summary>
    ///  Gets the name of the type this statistics instance is tracking.
    /// </summary>
    public string TypeName { get; }
    
    /// <summary>
    ///  Gets the total number of times an object has been requested from the pool.
    /// </summary>
    public long TotalGets => Interlocked.Read(ref _totalGets);
    
    /// <summary>
    ///  Gets the total number of times an object has been returned to the pool.
    /// </summary>
    public long TotalReturns => Interlocked.Read(ref _totalReturns);
    
    /// <summary>
    ///  Gets the rate of returned objects to requested objects.
    /// </summary>
    public double ReturnRate => TotalGets > 0 ? (double)TotalReturns / TotalGets : 1.0;
    
    /// <summary>
    ///  Gets the number of objects that have been requested but not yet returned to the pool.
    /// </summary>
    public long OutstandingObjects => TotalGets - TotalReturns;

    internal void IncrementGets() => Interlocked.Increment(ref _totalGets);
    internal void IncrementReturns() => Interlocked.Increment(ref _totalReturns);

    internal void Reset()
    {
        Interlocked.Exchange(ref _totalGets, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
    }

    /// <summary>
    ///  Returns a string that represents the current object.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{TypeName}: Gets={TotalGets}, Returns={TotalReturns}, " +
               $"Rate={ReturnRate:P0}, Outstanding={OutstandingObjects}";
    }
}
