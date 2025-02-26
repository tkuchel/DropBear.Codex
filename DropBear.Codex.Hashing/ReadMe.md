# DropBear.Codex.Hashing

A flexible, high-performance hashing library for .NET 8 featuring multiple hashing algorithms (Argon2, Blake2, Blake3, etc.), robust error handling through strongly-typed Results, and a fluent builder API for registering and retrieving hasher instances.

## Overview

DropBear.Codex.Hashing contains:
- **IHasher**: A consistent interface for hashing and verification (synchronous and asynchronous).
- **HashBuilder**: A fluent builder for retrieving and configuring IHasher instances by key.
- **Result<,>** pattern with typed errors (HashComputationError, HashVerificationError, etc.).
- Pre-built hashers: Argon2, Blake2, Blake3, ExtendedBlake3, FNV1A, Murmur3, SipHash, XXHash.
- Fluent configuration for salt, iterations, memory size, seeds, etc. (where applicable).
- Memory-efficient implementation using Span<T> and ArrayPool for reduced allocations.
- Constant-time comparisons for cryptographic operations to prevent timing attacks.
- Object pooling support for high-throughput scenarios.

## Installation

Use the .NET CLI to install:
```
dotnet add package DropBear.Codex.Hashing
```

## Key Components

### 1. HashBuilder

- **`HashBuilder`** is your main entry point for retrieving hashers by name (e.g., "argon2", "blake2").
- It pre-registers default hashers for the following keys: "argon2", "blake2", "blake3", "extended_blake3", "fnv1a", "murmur3", "siphash", "xxhash".
- You can also register custom hashers and enable pooling for performance.
- The updated builder supports object pooling for better performance in high-throughput scenarios.

#### Example

```csharp
var builder = new HashBuilder();

// Retrieve a built-in hasher (Argon2)
try
{
    var argonHasher = builder.GetHasher("argon2");
    // Now argonHasher supports Argon2 hashing/verification
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Failed to get argon2 hasher: {ex.Message}");
}

// Alternatively, try pattern with strongly-typed Result:
var hasherResult = builder.TryGetHasher("blake2");
if (hasherResult.IsSuccess)
{
    var blake2Hasher = hasherResult.Value;
    // Use blake2Hasher to hash or verify
}
else
{
    Console.WriteLine($"Error: {hasherResult.Error.Message}");
}

// Using pooled hashers for better performance
using (var pooledResult = builder.GetPooledHasher("xxhash"))
{
    if (pooledResult.IsSuccess)
    {
        var xxHasher = pooledResult.Value.Hasher;
        // Use xxHasher here - it will be automatically returned to the pool when disposed
    }
}
```

### 2. IHasher

All hashers implement `IHasher`, which defines these methods:
- `HashAsync(string input, CancellationToken)`, `Hash(string input)`
- `VerifyAsync(string input, string expectedHash, CancellationToken)`, `Verify(string input, string expectedHash)`
- `EncodeToBase64HashAsync(ReadOnlyMemory<byte>, CancellationToken)`, `EncodeToBase64Hash(byte[])`
- `VerifyBase64HashAsync(ReadOnlyMemory<byte>, string, CancellationToken)`, `VerifyBase64Hash(byte[], string)`
- `WithSalt`, `WithIterations`, `WithHashSize` (no-op if unsupported for a given algorithm)

**All** return a `Result<T, HashingError>` indicating success/failure, with typed error messages.

### 3. HashingError, HashComputationError, HashVerificationError

- **HashingError**: Base class for hashing-related errors.
- **HashComputationError**: Errors during hashing (e.g., empty input, invalid parameters, algorithm failure).
- **HashVerificationError**: Errors during verification (e.g., missing input, hash mismatch).

### 4. Built-in Hashers

1. **Argon2Hasher**: Cryptographic hashing with memory-hard features. Supports:
   - `.WithSalt(...)`
   - `.WithIterations(...)`
   - `.WithMemorySize(...)`
   - `.WithDegreeOfParallelism(...)`
   - `.WithHashSize(...)`

2. **Blake2Hasher**: High-speed cryptographic hashing, optionally with salt:
   - `.WithSalt(...)`
   - `.WithHashSize(...)`

3. **Blake3Hasher**: Next-gen, extremely fast cryptographic hashing:
   - `.WithHashSize(...)` (supports variable-length output)

4. **ExtendedBlake3Hasher**: Inherits from `Blake3Hasher` but adds static helper methods:
   - `IncrementalHash(IEnumerable<byte[]>)` and `IncrementalHashAsync(...)`
   - `GenerateMac(byte[] data, byte[] key)` and `GenerateMacAsync(...)`
   - `DeriveKey(byte[] context, byte[] inputKeyingMaterial)` and `DeriveKeyAsync(...)`
   - `HashStream(Stream inputStream)` and `HashStreamAsync(...)`
   - `HashFileAsync(string filePath, CancellationToken)` for efficient file hashing

5. **Fnv1AHasher**: Simple, fast, non-cryptographic hash function:
   - `.WithHashSize(...)` (supports 32-bit or 64-bit output)

6. **Murmur3Hasher**: Non-cryptographic, widely used for hash-based lookups:
   - `.WithSeed(uint seed)` for customizing the hash seed

7. **SipHasher**: 64-bit keyed hashing, great for short message MACs:
   - Default constructor generates a secure random 16-byte key
   - `.WithKey(byte[] key)` for custom 16-byte key

8. **XxHasher**: XXHash for fast non-cryptographic hashing:
   - `.WithSeed(ulong seed)` for customizing the hash seed
   - `.WithHashSize(...)` (supports 32-bit or 64-bit output)

### 5. BaseHasher

All hashers now inherit from an optimized `BaseHasher` class that provides:
- Memory-efficient operations using Span<T> and ArrayPool
- Constant-time comparisons for cryptographic verification
- Common implementation for verification and encoding methods
- Consistent logging patterns and error handling

## Usage Examples

### 1. Basic Hashing with Argon2

```csharp
var builder = new HashBuilder();

// Retrieve Argon2
var hasherResult = builder.TryGetHasher("argon2");
if (!hasherResult.IsSuccess)
{
    Console.WriteLine($"Error: {hasherResult.Error.Message}");
    return;
}

var argon = hasherResult.Value
    .WithSalt(null) // Argon2 will generate random salt if null
    .WithIterations(4)
    .WithMemorySize(1024 * 1024)
    .WithDegreeOfParallelism(4)
    .WithHashSize(16);

// Async hashing
var hashRes = await argon.HashAsync("MyPa55word");
if (hashRes.IsSuccess)
{
    Console.WriteLine($"Argon2 Hash = {hashRes.Value}");

    // Verify
    var verifyRes = await argon.VerifyAsync("MyPa55word", hashRes.Value);
    if (verifyRes.IsSuccess)
    {
        Console.WriteLine("Argon2 verification succeeded!");
    }
    else
    {
        Console.WriteLine($"Verification failed: {verifyRes.Error.Message}");
    }
}
else
{
    Console.WriteLine($"Hashing failed: {hashRes.Error.Message}");
}
```

### 2. Using Blake2 & Base64

```csharp
var blake2 = builder.GetHasher("blake2")
    .WithHashSize(32); // 32 bytes output

// Synchronous example
var input = "HelloWorld";
var hashResult = blake2.Hash(input);
if (hashResult.IsSuccess)
{
    Console.WriteLine($"Blake2 Hash (salt + hash, base64): {hashResult.Value}");
}

// Encoding arbitrary data to base64
var data = new byte[] {1, 2, 3, 4, 5};
var encodeRes = blake2.EncodeToBase64Hash(data);
if (encodeRes.IsSuccess)
{
    Console.WriteLine($"Blake2 base64: {encodeRes.Value}");
}
else
{
    Console.WriteLine($"Failed to encode base64: {encodeRes.Error.Message}");
}
```

### 3. Registering a Custom Hasher & Enabling Pooling

```csharp
var customBuilder = new HashBuilder();

// Register a custom hasher with key "myhasher"
customBuilder.RegisterHasher("myhasher", () => new Blake2Hasher().WithHashSize(64));

// Enable pooling for performance
customBuilder.EnablePoolingForHasher("myhasher", maxPoolSize: 16);

// Retrieve and use a pooled hasher
using (var pooledResult = customBuilder.GetPooledHasher("myhasher"))
{
    if (pooledResult.IsSuccess)
    {
        var myHasher = pooledResult.Value.Hasher;
        var result = myHasher.Hash("SomeString");
        if (result.IsSuccess)
        {
            Console.WriteLine($"Pooled custom hasher result: {result.Value}");
        }
    }
}
```

### 4. File Hashing with ExtendedBlake3Hasher

```csharp
// Efficiently hash a large file
var result = await ExtendedBlake3Hasher.HashFileAsync("path/to/large/file.iso");
if (result.IsSuccess)
{
    Console.WriteLine($"File hash: {result.Value}");
}
else
{
    Console.WriteLine($"Error hashing file: {result.Error.Message}");
}

// Stream processing with progress reporting
using var stream = File.OpenRead("path/to/large/file.bin");
var progressToken = new CancellationTokenSource();
var hashTask = ExtendedBlake3Hasher.HashStreamAsync(stream, progressToken.Token);

// To cancel the operation after timeout
progressToken.CancelAfter(TimeSpan.FromMinutes(5));

try
{
    var hash = await hashTask;
    Console.WriteLine($"Stream hash: {hash}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Hashing was canceled.");
}
```

## Error Handling with Results

- Each method returns `Result<T, HashingError>` or `Result<Unit, HashingError>`.
- Common error subtypes:
  - **HashComputationError** for hashing failures (EmptyInput, AlgorithmError, etc.)
  - **HashVerificationError** for verification failures (MissingSalt, HashMismatch, etc.)
- Example:

```csharp
var verifyRes = hasher.Verify("test", "someExpectedHash==");
if (verifyRes.IsSuccess)
{
    Console.WriteLine("Verified successfully!");
}
else
{
    Console.WriteLine($"Verification error: {verifyRes.Error.Message}");

    // Check specific error type
    if (verifyRes.Error is HashVerificationError hve)
    {
        // handle mismatch or invalid format
    }
}
```

## HashingHelper

`HashingHelper` offers static convenience methods:
- `GenerateRandomSalt(size)`
- `CombineBytes(salt, hash)` and `CombineBytes(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` to store salt+hash
- `ExtractBytes` to separate salt+hash
- `HashStreamChunkedAsync` for chunked hashing of large data
- `ConvertByteArrayToBase64String`, etc.

## Performance Optimization

This library uses several optimization techniques:
- **Span<T>** and **ReadOnlySpan<T>** for zero-allocation memory operations
- **ArrayPool<T>** for efficient buffer management
- **Constant-time comparisons** to prevent timing attacks
- **FrozenDictionary** for fast, immutable lookups
- **Smart async/sync dispatch** based on input size
- **Object pooling** for high-throughput scenarios

## Contributing

Contributions via Pull Requests or Issues are welcome. Please ensure your changes align with the library's coding standards and pass existing tests.

## License

DropBear.Codex.Hashing is licensed under the MIT License. See the LICENSE file for details.
