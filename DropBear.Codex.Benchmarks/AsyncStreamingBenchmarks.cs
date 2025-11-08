using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Benchmarks for comparing async streaming patterns:
///     Full materialization vs incremental streaming with IAsyncEnumerable.
/// </summary>
/// <remarks>
///     <strong>Important</strong>: Run benchmarks SEQUENTIALLY, not in parallel.
///     Parallel execution causes file locking issues with BenchmarkDotNet's auto-generated code.
///     Use: <code>dotnet run -c Release -- --filter "*AsyncStreamingBenchmarks*"</code>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 50)]
public class AsyncStreamingBenchmarks
{
    private const int ItemCount = 10000;

    #region Benchmark: Full Materialization vs Streaming (Take 10)

    [Benchmark(Baseline = true, Description = "Materialize All then Take 10")]
    public async Task<List<int>> MaterializeAll_ThenTake10()
    {
        var result = await GenerateNumbersAsync(ItemCount);
        return result.Take(10).ToList();
    }

    [Benchmark(Description = "Stream and Take 10 (Early Exit)")]
    public async Task<List<int>> StreamAndTake10()
    {
        var stream = StreamNumbersAsync(ItemCount);
        var result = new List<int>();
        var count = 0;

        await foreach (var number in stream.WithCancellation(CancellationToken.None))
        {
            result.Add(number);
            if (++count >= 10) break; // Early exit after 10 items
        }

        return result;
    }

    #endregion

    #region Benchmark: AsyncEnumerableResult Pattern

    [Benchmark(Description = "AsyncEnumerableResult: ToListAsync")]
    public async Task<List<int>> AsyncEnumerableResult_ToList()
    {
        var stream = StreamNumbersAsResult(ItemCount);
        var listResult = await stream.ToListAsync();
        if (listResult.Value != null)
        {
            return listResult.IsSuccess ? listResult.Value.ToList() : [];
        }

        return [];
    }

    [Benchmark(Description = "AsyncEnumerableResult: Stream + Filter + Take")]
    public async Task<List<int>> AsyncEnumerableResult_FilterAndTake()
    {
        var stream = StreamNumbersAsResult(ItemCount);
        var filtered = stream.Where(x => x % 2 == 0).Take(100);

        var result = new List<int>();
        await foreach (var number in filtered.WithCancellation(CancellationToken.None))
        {
            result.Add(number);
        }

        return result;
    }

    #endregion

    #region Benchmark: Memory Usage Comparison

    [Benchmark(Description = "List<T>: Add 10k items")]
    public List<int> List_Add10k()
    {
        var list = new List<int>();
        for (var i = 0; i < ItemCount; i++)
        {
            list.Add(i);
        }
        return list;
    }

    [Benchmark(Description = "Stream: Yield 10k items (consumed)")]
    public async Task<int> Stream_Yield10k()
    {
        var count = 0;
        await foreach (var _ in StreamNumbersAsync(ItemCount))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Helper Methods

    private static async Task<List<int>> GenerateNumbersAsync(int count)
    {
        await Task.Delay(1); // Simulate async work
        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(i);
        }
        return list;
    }

    private static async IAsyncEnumerable<int> StreamNumbersAsync(int count)
    {
        await Task.Delay(1); // Simulate async work
        for (var i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    private static AsyncEnumerableResult<int, SimpleError> StreamNumbersAsResult(int count)
    {
        async IAsyncEnumerable<int> Generate()
        {
            await Task.Delay(1); // Simulate async work
            for (var i = 0; i < count; i++)
            {
                yield return i;
            }
        }

        return AsyncEnumerableResult<int, SimpleError>.Success(Generate());
    }

    #endregion
}
