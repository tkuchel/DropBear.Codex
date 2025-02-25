# DropBear.Codex.Hashing

A flexible, high-performance hashing library for .NET 8 featuring multiple hashing algorithms (Argon2, Blake2, Blake3, etc.), robust error handling through strongly-typed Results, and a fluent builder API for registering and retrieving hasher instances.

## Overview

DropBear.Codex.Hashing contains:
- **IHasher**: A consistent interface for hashing and verification (synchronous and asynchronous).
- **HashBuilder**: A fluent builder for retrieving and configuring IHasher instances by key.
- **Result<,>** pattern with typed errors (HashComputationError, HashVerificationError, etc.).
- Pre-built hashers: Argon2, Blake2, Blake3, ExtendedBlake3, FNV1A, Murmur3, SipHash, XXHash.
- Fluent configuration for salt, iterations, memory size, seeds, etc. (where applicable).

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

2. **Blake2Hasher**: High-speed hashing, optionally with salt.

3. **Blake3Hasher**: Next-gen, extremely fast. Doesn’t support salt or iterations.

4. **ExtendedBlake3Hasher**: Inherits from `Blake3Hasher` but adds static helper methods for incremental hashing, MAC generation, key derivation, and stream hashing.

5. **Fnv1AHasher**: Simple, fast, non-cryptographic. No salt or iteration.

6. **Murmur3Hasher**: Non-cryptographic, widely used for hash-based lookups. Optional seed.

7. **SipHasher**: 64-bit keyed hashing (requires a 16-byte key). Great for short message MACs.

8. **XxHasher**: XXHash (64-bit) for fast non-cryptographic hashing. Optional 64-bit seed.

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

// Optionally enable pooling for performance
customBuilder.EnablePoolingForHasher("myhasher", maxPoolSize: 16);

// Retrieve from the pool
var myHasher = customBuilder.GetHasher("myhasher");
var result = myHasher.Hash("SomeString");
if (result.IsSuccess)
{
    Console.WriteLine($"Pooled custom hasher result: {result.Value}");
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
- `CombineBytes(salt, hash)` to store salt+hash
- `ExtractBytes` to separate salt+hash
- `HashStreamChunkedAsync` for chunked hashing
- `ConvertByteArrayToBase64String`, etc.

## Contributing

Contributions via Pull Requests or Issues are welcome. Please ensure your changes align with the library's coding standards and pass existing tests.

## License

DropBear.Codex.Hashing is licensed under the MIT License. See the LICENSE file for details.
