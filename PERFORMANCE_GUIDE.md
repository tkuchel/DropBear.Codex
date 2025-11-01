# DropBear.Codex Performance Guide

**Last Updated**: 2025-10-31
**Version**: 2025.10.x
**Target Framework**: .NET 9.0

---

## Table of Contents

1. [Quick Reference](#quick-reference)
2. [Serialization Performance](#serialization-performance)
3. [Hashing Performance](#hashing-performance)
4. [Result Pattern Performance](#result-pattern-performance)
5. [Streaming Performance](#streaming-performance)
6. [Decision Trees](#decision-trees)
7. [Detailed Benchmark Results](#detailed-benchmark-results)
8. [Performance Best Practices](#performance-best-practices)

---

## Quick Reference

### Fastest Options by Category

| Category | Use Case | Recommended Implementation | Why |
|----------|----------|---------------------------|-----|
| **Serialization (Small)** | Write-heavy workload | **MessagePack** (308ns) | 113x faster serialize |
| **Serialization (Small)** | Read-heavy workload | **JSON** (536ns) | 147x faster deserialize |
| **Serialization (Large)** | Any workload | **JSON** (114μs) | 17x faster, MessagePack fails |
| **Serialization (Streaming)** | Early exit / Pagination | **JSON Streaming** | 56-59% faster for <10% data |
| **Hashing (Small)** | Best all-around | **Blake3** (309ns, 0B) | 122x faster, zero allocations |
| **Hashing (Large)** | Non-cryptographic | **XxHash** (83μs) | 1.5x faster than Blake3 |
| **Hashing (Large)** | Cryptographic | **Blake3** (127μs) | Fast + secure |
| **Error Handling** | Error rate < 1% | **Exceptions** (225ns) | 136x faster happy path |
| **Error Handling** | Error rate > 1% | **Result<T>** (113μs) | 21x faster error path |
| **Collections** | Full materialization | **ToList() / ToArray()** | No streaming overhead |
| **Collections** | Early exit / Pagination | **IAsyncEnumerable** | 56-59% faster for <10% data |

---

## Serialization Performance

### Overview

DropBear.Codex supports multiple serialization formats:
- **JSON** (System.Text.Json): Cross-platform, human-readable, good performance
- **MessagePack**: Binary format, highest performance, smaller payload
- **Encrypted**: Secure serialization using AES encryption
- **Compressed**: GZip/Deflate compression with serialization

### Performance Comparison

#### Small Objects (~1KB)

| Format | Serialize | Deserialize | Size | When to Use |
|--------|-----------|-------------|------|-------------|
| **MessagePack** | **308 ns** ✅ | 79 μs | ~1KB binary | Performance-critical, trusted endpoints |
| **JSON** | 35 μs | **536 ns** ✅ | ~1KB text | Web APIs, cross-platform, debugging |
| **JSON (Streaming)** | N/A | ~27 μs (100 items) | ~1KB text | Large arrays, pagination |

**Key Insights:**
- MessagePack **113x faster** for small serialize (308ns vs 35μs)
- JSON **147x faster** for small deserialize (536ns vs 79μs)
- **Recommendation**: Use MessagePack for write-heavy, JSON for read-heavy workloads

#### Large Objects (~100KB)

| Format | Serialize | Deserialize | Size | When to Use |
|--------|-----------|-------------|------|-------------|
| **MessagePack** | 1.92 ms | *Test failed* | ~100KB binary | Bulk data transfer |
| **JSON** | **114 μs** ✅ | *Test failed* | ~100KB text | Standard REST APIs |
| **JSON (Streaming)** | N/A | 188 μs (1000 items) | ~100KB text | Memory-constrained scenarios |

**Key Insights:**
- JSON **17x faster** for large serialize (114μs vs 1.92ms)
- Deserialize benchmarks failed for large objects (investigation needed)
- **Recommendation**: Use JSON for large object serialization until MessagePack issues resolved

### Recommendations

#### ✅ Use JSON When:
- Building REST APIs with standard HTTP clients
- Need human-readable output for debugging
- Cross-platform compatibility is critical
- Working with JavaScript/TypeScript frontends
- Small payload size difference is acceptable

#### ✅ Use MessagePack When:
- Both endpoints control the format (internal services)
- Performance is critical (high-throughput scenarios)
- Binary format is acceptable
- Payload size matters (mobile apps, IoT)
- Not primarily web-based communication

#### ✅ Use Streaming When:
- Dataset > 100 items AND consuming < 10%
- Implementing pagination or search
- Memory-constrained environments
- Progressive UI rendering
- See [Streaming Performance](#streaming-performance) for details

---

## Hashing Performance

### Overview

DropBear.Codex provides multiple hashing algorithms optimized for different use cases:
- **Blake2**: Cryptographic hash, faster than SHA256
- **XxHash64**: Non-cryptographic, extremely fast
- **FNV1A**: Simple, low-memory footprint

### Performance Comparison

#### Small Data (100 bytes)

| Algorithm | Speed | Memory | Security | When to Use |
|-----------|-------|--------|----------|-------------|
| **Blake3** | **309 ns** ✅ | 0 B | High | Best all-around: fast + secure |
| **XxHash** | 38 μs | 96 KB | None | Hash tables, checksums |
| **SHA256** | 1.97 ms | 320 KB | High | FIPS compliance required |

**Key Insights:**
- Blake3 **122x faster** than XxHash for small data (309ns vs 38μs)
- Blake3 **6,380x faster** than SHA256 (309ns vs 1.97ms)
- Blake3 has **zero allocations** - extremely efficient
- **Recommendation**: Use Blake3 for all use cases unless FIPS compliance required

#### Medium Data (1MB)

| Algorithm | Speed | Memory | Security | When to Use |
|-----------|-------|--------|----------|-------------|
| **XxHash** | 83 μs | 192 KB | None | Non-cryptographic checksums |
| **Blake3** | 127 μs | 272 KB | High | Fast cryptographic hashing |
| **SHA256** | *Test failed* | *Test failed* | High | FIPS compliance |

**Key Insights:**
- XxHash **1.5x faster** than Blake3 for medium data (83μs vs 127μs)
- For medium/large data, non-cryptographic advantage appears
- SHA256 benchmarks failed (investigation needed)
- **Recommendation**: Use XxHash for non-crypto, Blake3 for crypto needs

### Recommendations

#### ✅ Use XxHash64 When:
- Non-cryptographic hashing is sufficient
- Maximum speed is critical
- Hash table implementations
- Quick data integrity checks
- Cache key generation

#### ✅ Use Blake2 When:
- Security matters (password hashing, signatures)
- Cryptographic strength required
- Better performance than SHA256/SHA512 needed
- FIPS compliance not required

#### ✅ Use FNV1A When:
- Extremely memory-constrained environments
- Simple hash function needed
- No cryptographic requirements
- Legacy compatibility

---

## Result Pattern Performance

### Overview

The Result<T> pattern provides type-safe error handling as an alternative to exceptions. This section compares the performance characteristics of both approaches.

### Performance Comparison

| Operation | Result<T> | Exception | Winner | Performance Gap |
|-----------|-----------|-----------|--------|-----------------|
| **Success Path (Happy)** | 31 μs | **225 ns** ✅ | Exception | **136x faster** |
| **Error Path (Expected)** | **113 μs** ✅ | 2.37 ms | Result<T> | **21x faster** |
| **Chain Operations** | 73 μs | 377 ns | Exception† | *See notes* |

**† Important Notes:**
- Manual chaining (no Result): 377ns - extremely fast
- Result chain (Map/Bind): 73μs - adds overhead for safety
- Exception-based error path: 2.37ms - **extremely expensive**

### THE CRITICAL INSIGHT 🎯

**Use Exceptions When:**
- **Happy path is 99%+** of executions
- Errors are truly exceptional (system failures, programmer errors)
- Performance penalty acceptable for rare error cases
- Example: `int.Parse()` throwing `FormatException`

**Use Result<T> When:**
- **Errors are expected** (validation, not found, business rules)
- Error rate > 1% of executions
- 21x performance gain on error path justifies 136x penalty on success
- Example: `TryParse()`, user input validation, optional operations

### Break-Even Analysis

Given error rate `E` (as fraction 0-1):
- **Exception cost**: `225ns + (E × 2,370,000ns)`
- **Result<T> cost**: `31,000ns` (constant)

Break-even when: `E > 1.3%`

**Recommendation Matrix:**

| Error Rate | Recommended Approach | Reason |
|------------|---------------------|---------|
| < 0.1% | **Exceptions** | Happy path dominates, errors rare |
| 0.1% - 1% | **Exceptions** | Still below break-even point |
| 1% - 5% | **Result<T>** ⚠️ | Near break-even, consider error handling needs |
| 5% - 20% | **Result<T>** ✅ | Clear performance win |
| > 20% | **Result<T>** ✅ | Massive performance advantage |

### Recommendations

#### ✅ Use Result<T> When:
- Errors are **expected** and common (validation, not found, etc.)
- Error handling is part of business logic
- Functional programming style preferred
- Composing multiple operations with Map/Bind
- Avoiding try-catch overhead

**Example:**
```csharp
public Result<User, UserError> GetUser(int id)
{
    var user = _repository.Find(id);
    return user is not null
        ? Result<User, UserError>.Success(user)
        : Result<User, UserError>.Failure(UserError.NotFound(id));
}
```

#### ✅ Use Exceptions When:
- Errors are **exceptional** and rare
- Error represents programmer error or system failure
- Integrating with frameworks that expect exceptions
- Stack traces are critical for debugging
- Error occurs deep in call stack

**Example:**
```csharp
public User GetUserOrThrow(int id)
{
    var user = _repository.Find(id);
    if (user is null)
        throw new InvalidOperationException($"User {id} must exist");
    return user;
}
```

### Performance Impact

**Key Insight**: The Result<T> pattern has a **constant overhead of ~31μs** regardless of success or failure. Exceptions have **variable cost**: 225ns on success, but **2.37ms on error** (10,000x worse). This makes Result<T> dramatically better when errors occur frequently (>1% of the time), but exceptions better when errors are truly rare (<1%).

---

## Streaming Performance

### Overview

IAsyncEnumerable streaming allows lazy evaluation and early-exit scenarios, but introduces overhead when consuming entire datasets.

### Detailed Results

**Reference**: See [BENCHMARK_RESULTS_STREAMING.md](./BENCHMARK_RESULTS_STREAMING.md) for comprehensive analysis.

### Quick Summary

| Scenario | Full Deserialize | Streaming | Winner | Performance Gain |
|----------|------------------|-----------|--------|------------------|
| **Consume All (1000 items)** | 187.58 μs | 265.32 μs | Full Deserialize | Streaming 41% slower |
| **Take 100 from 1000** | 187.58 μs | 81.61 μs | Streaming | **56% faster** |
| **Take 10 from 1000** | 187.58 μs | 76.83 μs | Streaming | **59% faster** |
| **Consume All (100 items)** | 17.80 μs | 26.84 μs | Full Deserialize | Streaming 51% slower |

### Decision Matrix

| Dataset Size | Consumption % | Recommended Approach | Reason |
|--------------|---------------|---------------------|---------|
| < 100 items | Any | Full Deserialization | Overhead exceeds benefits |
| > 100 items | 100% | Full Deserialization | Streaming adds 41% overhead |
| > 100 items | 10-20% | Streaming | 50-60% performance gain |
| > 1000 items | 1-5% | Streaming | Maximum efficiency |

### Code Examples

#### ❌ Anti-Pattern: Streaming + Full Materialization
```csharp
// Don't do this - combines worst of both worlds
var items = await serializer
    .DeserializeAsyncEnumerable<T>(stream)
    .ToListAsync(); // Pays streaming overhead + materializes anyway
```

#### ✅ Good: Full Deserialization
```csharp
// When you need all items
var result = await serializer.DeserializeAsync<List<T>>(stream);
if (result.IsSuccess)
{
    var items = result.Value; // All items available
}
```

#### ✅ Good: Streaming with Early Exit
```csharp
// When you might exit early
await foreach (var itemResult in serializer.DeserializeAsyncEnumerable<T>(stream))
{
    if (itemResult.IsSuccess)
    {
        ProcessItem(itemResult.Value);
        if (ShouldStop()) break; // Key: early exit benefit
    }
}
```

---

## Async Streaming Performance

### Overview

This section covers performance characteristics of IAsyncEnumerable patterns beyond serialization, including general async streaming best practices.

### Performance Comparison

[PENDING - To be populated after AsyncStreamingBenchmarks complete]

| Pattern | Performance | Memory | When to Use |
|---------|-------------|--------|-------------|
| **ToListAsync()** | [PENDING] | [PENDING] | Need all items in memory |
| **Take(N)** | [PENDING] | [PENDING] | Known item count needed |
| **TakeWhile(predicate)** | [PENDING] | [PENDING] | Conditional early exit |
| **Where + Take** | [PENDING] | [PENDING] | Filtered subset |

---

## Decision Trees

### Serialization Format Selection

```
START: Need to serialize data
│
├─ Is performance critical?
│  ├─ YES: Do both endpoints control the format?
│  │  ├─ YES: Use MessagePack ✅
│  │  └─ NO: Use JSON (compatibility matters)
│  │
│  └─ NO: Is debugging important?
│     ├─ YES: Use JSON ✅ (human-readable)
│     └─ NO: Use MessagePack (smaller payload)
│
└─ Need encryption?
   ├─ YES: Use EncryptedSerializer ✅
   └─ NO: Use JSON or MessagePack
```

### Hashing Algorithm Selection

```
START: Need to hash data
│
├─ Is security required?
│  ├─ YES: Use Blake2 ✅ (cryptographic)
│  └─ NO: Is maximum speed needed?
│     ├─ YES: Use XxHash64 ✅
│     └─ NO: Use FNV1A (low memory)
```

### Error Handling Approach

```
START: Need to handle errors
│
├─ Is error expected/common?
│  ├─ YES: Use Result<T> ✅
│  └─ NO: Is it exceptional condition?
│     ├─ YES: Use Exception ✅
│     └─ NO: Is composability needed?
│        ├─ YES: Use Result<T> (Map/Bind)
│        └─ NO: Either approach fine
```

### Streaming vs Full Materialization

```
START: Need to process collection
│
├─ Dataset size?
│  ├─ < 100 items: Full materialization ✅
│  └─ > 100 items: Will you consume all?
│     ├─ YES: Full materialization ✅
│     └─ NO: Use streaming ✅
```

---

## Detailed Benchmark Results

### Methodology

All benchmarks run using BenchmarkDotNet with the following configuration:
- **.NET Version**: 9.0.10
- **Configuration**: Release mode
- **Iterations**: 20-50 per benchmark
- **Outlier Removal**: Enabled
- **Memory Diagnostics**: Enabled (Gen0, Gen1, Allocated)
- **Confidence Level**: 99.9%

### Environment

```
Runtime: .NET 9.0.10 (9.0.10, 9.0.1025.47515)
Architecture: X64 RyuJIT x86-64-v4
GC: Concurrent Workstation
Hardware Intrinsics: AVX512, AVX2, SSE4.2
```

### Raw Results

#### Serialization Benchmarks
[PENDING - To be populated from serialization_benchmarks_results.txt]

#### Hashing Benchmarks
[PENDING - To be populated from hashing_benchmarks_results.txt]

#### Result Pattern Benchmarks
[PENDING - To be populated from result_pattern_benchmarks_results.txt]

#### Async Streaming Benchmarks
[PENDING - To be populated from async_streaming_benchmarks_results.txt]

---

## Performance Best Practices

### General Guidelines

1. **Measure First**: Always benchmark your specific use case before optimizing
2. **Profile Production**: Dev machine performance may differ from production
3. **Consider Trade-offs**: Fastest isn't always best (maintainability, security)
4. **Batch Operations**: Reduce per-item overhead for large datasets
5. **Use Pooling**: Reuse expensive objects (connections, buffers, serializers)

### Serialization Best Practices

```csharp
// ✅ Good: Reuse serializer instances
private readonly ISerializer _serializer = new JsonSerializer();

public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T obj)
{
    return await _serializer.SerializeAsync(obj); // Reuses internal buffers
}

// ❌ Bad: Creating serializer per call
public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T obj)
{
    var serializer = new JsonSerializer(); // Allocates every time
    return await serializer.SerializeAsync(obj);
}
```

### Hashing Best Practices

```csharp
// ✅ Good: Use HashBuilder for complex scenarios
var hashBuilder = new HashBuilder();
var hasher = hashBuilder
    .WithAlgorithm<XxHasher>()
    .WithSalt("my-salt")
    .Build();

var hash = await hasher.ComputeHashAsync(data); // Optimized

// ✅ Good: Direct hasher for simple cases
var hasher = new XxHasher();
var hash = await hasher.ComputeHashAsync(data); // Less overhead
```

### Result Pattern Best Practices

```csharp
// ✅ Good: Chain operations with Map/Bind
return await GetUserAsync(id)
    .MapAsync(user => EnrichUserAsync(user))
    .BindAsync(user => ValidateUserAsync(user))
    .MapAsync(user => user.ToDto());

// ✅ Good: Early return on failure
var userResult = await GetUserAsync(id);
if (!userResult.IsSuccess)
    return Result<UserDto, Error>.Failure(userResult.Error);

var user = userResult.Value;
// Continue processing...
```

### Streaming Best Practices

```csharp
// ✅ Good: Stream with clear early exit
await foreach (var item in GetItemsAsync().WithCancellation(cancellationToken))
{
    if (await ProcessItemAsync(item))
        break; // Clear benefit from streaming
}

// ❌ Bad: Stream without early exit possibility
var items = await GetItemsAsync().ToListAsync(); // Just materialize directly
```

---

## Benchmarking Your Code

### Running Benchmarks

To run all benchmarks:
```bash
dotnet run --project DropBear.Codex.Benchmarks -c Release
```

To run specific benchmark suite:
```bash
dotnet run --project DropBear.Codex.Benchmarks -c Release -- --filter "*Serialization*"
```

### Creating Custom Benchmarks

Example benchmark for your use case:

```csharp
using BenchmarkDotNet.Attributes;
using DropBear.Codex.Serialization;

[MemoryDiagnoser]
[SimpleJob(iterationCount: 20)]
public class MyBenchmark
{
    private ISerializer _serializer = null!;
    private MyData _testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _serializer = new JsonSerializer();
        _testData = new MyData { /* ... */ };
    }

    [Benchmark(Baseline = true)]
    public async Task<byte[]> Current_Approach()
    {
        var result = await _serializer.SerializeAsync(_testData);
        return result.Value;
    }

    [Benchmark]
    public async Task<byte[]> Optimized_Approach()
    {
        // Your optimized code
    }
}
```

---

## Continuous Performance Monitoring

### Regression Testing

Consider setting up automated benchmark runs on CI/CD to catch performance regressions:

```yaml
# GitHub Actions example
- name: Run Benchmarks
  run: dotnet run --project DropBear.Codex.Benchmarks -c Release

- name: Check for Regressions
  run: |
    # Compare with baseline
    # Fail if > 10% slower
```

### Performance Budgets

Recommended performance budgets for DropBear.Codex operations:

| Operation | Target | Threshold |
|-----------|--------|-----------|
| JSON Serialize (1KB) | < 50 μs | < 100 μs |
| MessagePack Serialize (1KB) | < 30 μs | < 60 μs |
| XxHash (1KB) | < 5 μs | < 10 μs |
| Result<T> Success Path | < 1 μs | < 2 μs |

---

## Contributing

Found a performance issue or optimization? Please:
1. Create a benchmark demonstrating the issue
2. Profile with dotTrace or PerfView
3. Submit a PR with measurements showing improvement
4. Update this guide with new findings

---

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Tips](https://learn.microsoft.com/en-us/dotnet/framework/performance/)
- [System.Text.Json Performance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/performance)
- [MessagePack Specification](https://msgpack.org/)

---

**Last Updated**: 2025-10-31
**Next Review**: When new features are added or performance characteristics change
