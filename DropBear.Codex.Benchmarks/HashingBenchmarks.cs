using BenchmarkDotNet.Attributes;
using DropBear.Codex.Hashing;
using DropBear.Codex.Hashing.Interfaces;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Benchmarks for comparing hashing algorithm performance:
///     Blake3 vs XxHash vs SHA256.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100)]
public class HashingBenchmarks
{
    private byte[] _smallData = null!;
    private byte[] _mediumData = null!;
    private byte[] _largeData = null!;
    private IHasher _blake3Hasher = null!;
    private IHasher _xxHashHasher = null!;
    private IHasher _blake2Hasher = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small data: 100 bytes
        _smallData = new byte[100];
        Random.Shared.NextBytes(_smallData);

        // Medium data: 1 MB
        _mediumData = new byte[1024 * 1024];
        Random.Shared.NextBytes(_mediumData);

        // Large data: 10 MB
        _largeData = new byte[10 * 1024 * 1024];
        Random.Shared.NextBytes(_largeData);

        // Pre-create hashers to avoid overhead in benchmark iterations
        var hashBuilder = new HashBuilder();
        _blake3Hasher = hashBuilder.GetHasher("blake3");
        _xxHashHasher = hashBuilder.GetHasher("xxhash");
        _blake2Hasher = hashBuilder.GetHasher("blake2");
    }

    #region Small Data (100 bytes)

    [Benchmark(Baseline = true, Description = "Blake3: Small (100 bytes)")]
    public string? Blake3_Small()
    {
        var result = _blake3Hasher.EncodeToBase64Hash(_smallData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "XxHash: Small (100 bytes)")]
    public string? XxHash_Small()
    {
        var result = _xxHashHasher.EncodeToBase64Hash(_smallData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "Blake2: Small (100 bytes)")]
    public string? Blake2_Small()
    {
        var result = _blake2Hasher.EncodeToBase64Hash(_smallData);
        return result.IsSuccess ? result.Value : null;
    }

    #endregion

    #region Medium Data (1 MB)

    [Benchmark(Description = "Blake3: Medium (1 MB)")]
    public string? Blake3_Medium()
    {
        var result = _blake3Hasher.EncodeToBase64Hash(_mediumData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "XxHash: Medium (1 MB)")]
    public string? XxHash_Medium()
    {
        var result = _xxHashHasher.EncodeToBase64Hash(_mediumData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "Blake2: Medium (1 MB)")]
    public string? Blake2_Medium()
    {
        var result = _blake2Hasher.EncodeToBase64Hash(_mediumData);
        return result.IsSuccess ? result.Value : null;
    }

    #endregion

    #region Large Data (10 MB)

    [Benchmark(Description = "Blake3: Large (10 MB)")]
    public string? Blake3_Large()
    {
        var result = _blake3Hasher.EncodeToBase64Hash(_largeData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "XxHash: Large (10 MB)")]
    public string? XxHash_Large()
    {
        var result = _xxHashHasher.EncodeToBase64Hash(_largeData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "Blake2: Large (10 MB)")]
    public string? Blake2_Large()
    {
        var result = _blake2Hasher.EncodeToBase64Hash(_largeData);
        return result.IsSuccess ? result.Value : null;
    }

    #endregion
}
