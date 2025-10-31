using BenchmarkDotNet.Attributes;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Serializers;
using SystemJsonSerializer = System.Text.Json.JsonSerializer;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Benchmarks for Serialization streaming performance.
///     Compares streaming deserialization vs full materialization.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 20)]
public class SerializationStreamingBenchmarks
{
    private IStreamingSerializer _streamingSerializer = null!;
    private byte[] _largeJsonArray = null!;
    private byte[] _mediumJsonArray = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup serialization streaming
        _streamingSerializer = new JsonStreamingDeserializer();

        // Create large dataset (1000 records)
        var largeData = Enumerable.Range(0, 1000).Select(i => new DataRecord { Id = i, Name = $"Record{i}", Value = i * 2.5 }).ToList();
        _largeJsonArray = SystemJsonSerializer.SerializeToUtf8Bytes(largeData);

        // Create medium dataset (100 records)
        var mediumData = Enumerable.Range(0, 100).Select(i => new DataRecord { Id = i, Name = $"Record{i}", Value = i * 2.5 }).ToList();
        _mediumJsonArray = SystemJsonSerializer.SerializeToUtf8Bytes(mediumData);
    }

    #region Large Dataset Benchmarks (1000 records)

    [Benchmark(Baseline = true, Description = "Full Deserialize: 1000 records")]
    public async Task<int> Large_FullDeserialize()
    {
        using var stream = new MemoryStream(_largeJsonArray);
        var records = await SystemJsonSerializer.DeserializeAsync<List<DataRecord>>(stream);
        return records?.Count ?? 0;
    }

    [Benchmark(Description = "Stream All: 1000 records (count only)")]
    public async Task<int> Large_StreamAll_CountOnly()
    {
        using var stream = new MemoryStream(_largeJsonArray);
        var count = 0;

        await foreach (var result in _streamingSerializer.DeserializeAsyncEnumerable<DataRecord>(stream))
        {
            if (result.IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(Description = "Stream & Take 100: Early exit from 1000")]
    public async Task<int> Large_StreamAndTake100()
    {
        using var stream = new MemoryStream(_largeJsonArray);
        var count = 0;

        await foreach (var result in _streamingSerializer.DeserializeAsyncEnumerable<DataRecord>(stream))
        {
            if (result.IsSuccess)
            {
                count++;
                if (count >= 100) break; // Early exit after 100 items
            }
        }

        return count;
    }

    [Benchmark(Description = "Stream & Take 10: Early exit from 1000")]
    public async Task<int> Large_StreamAndTake10()
    {
        using var stream = new MemoryStream(_largeJsonArray);
        var count = 0;

        await foreach (var result in _streamingSerializer.DeserializeAsyncEnumerable<DataRecord>(stream))
        {
            if (result.IsSuccess)
            {
                count++;
                if (count >= 10) break; // Early exit after 10 items
            }
        }

        return count;
    }

    #endregion

    #region Medium Dataset Benchmarks (100 records)

    [Benchmark(Description = "Full Deserialize: 100 records")]
    public async Task<int> Medium_FullDeserialize()
    {
        using var stream = new MemoryStream(_mediumJsonArray);
        var records = await SystemJsonSerializer.DeserializeAsync<List<DataRecord>>(stream);
        return records?.Count ?? 0;
    }

    [Benchmark(Description = "Stream All: 100 records (count only)")]
    public async Task<int> Medium_StreamAll_CountOnly()
    {
        using var stream = new MemoryStream(_mediumJsonArray);
        var count = 0;

        await foreach (var result in _streamingSerializer.DeserializeAsyncEnumerable<DataRecord>(stream))
        {
            if (result.IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    #endregion

    #region Helper Classes

    public record DataRecord
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public double Value { get; init; }
    }

    #endregion
}
