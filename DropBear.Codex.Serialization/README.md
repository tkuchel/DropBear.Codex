# DropBear.Codex.Serialization

A high-performance, memory-optimized serialization library for .NET 9 applications that supports JSON, MessagePack, and custom serialization with compression, encoding, and encryption capabilities.

## Overview

DropBear.Codex.Serialization is a comprehensive serialization framework designed to provide a consistent, flexible approach to data serialization. It features robust error handling using a Result pattern, memory optimization through buffer pooling, and support for various serialization formats, compression, encoding, and encryption strategies.

The library is built with a focus on performance and memory efficiency, making it suitable for both small and large-scale applications that require reliable and secure data serialization.

## Features

- **Robust Error Handling**: Uses a strongly-typed Result pattern for predictable error handling
- **Memory Optimization**: Implements buffer pooling and recyclable memory streams to minimize GC pressure
- **Multiple Serialization Formats**:
  - JSON serialization with configurable options
  - MessagePack serialization for compact binary format
  - Custom serialization extensibility
- **Compression Support**:
  - GZip compression for maximum compatibility
  - Deflate compression for better performance
  - Pluggable architecture for custom compression algorithms
- **Encoding Options**:
  - Base64 encoding for text-safe binary data
  - Hexadecimal encoding for human-readable binary data
- **Encryption Capabilities**:
  - AES-GCM encryption for authenticated encryption
  - AES-CNG encryption for Windows platforms
  - RSA key management for secure key exchange
- **Builder Pattern API**: Fluent, easy-to-use API for constructing serializers
- **Async/Await First**: Fully asynchronous API with proper cancellation token support
- **Performance Optimized**: Uses .NET 9 features for maximum performance
- **Stream Support**: Direct stream serialization for large data sets

## Installation

```
dotnet add package DropBear.Codex.Serialization
```

## Dependencies

- .NET 9.0 or higher
- Microsoft.IO.RecyclableMemoryStream
- MessagePack (optional, for MessagePack serialization)
- Serilog
- DropBear.Codex.Core

## Basic Usage

### Basic JSON Serialization

```csharp
// Create a serializer with default JSON options
var serializerBuilder = new SerializationBuilder()
    .WithDefaultJsonSerializerOptions()
    .Build();

if (serializerBuilder.IsSuccess)
{
    var serializer = serializerBuilder.Value;
    
    // Serialize an object
    var data = new Person { Name = "John Doe", Age = 30 };
    var serializeResult = await serializer.SerializeAsync(data);
    
    if (serializeResult.IsSuccess)
    {
        // Store or transmit the serialized data
        byte[] serializedData = serializeResult.Value;
        
        // Deserialize the data back
        var deserializeResult = await serializer.DeserializeAsync<Person>(serializedData);
        
        if (deserializeResult.IsSuccess)
        {
            Console.WriteLine($"Deserialized person: {deserializeResult.Value.Name}, {deserializeResult.Value.Age}");
        }
    }
}
```

### With Compression and Encryption

```csharp
// Create a serializer with compression and encryption
var serializerBuilder = new SerializationBuilder()
    .WithDefaultJsonSerializerOptions()
    .WithCompression<GZipCompressionProvider>()
    .WithEncryption<AESGCMEncryptionProvider>()
    .WithKeys("path/to/public.key", "path/to/private.key")
    .Build();

if (serializerBuilder.IsSuccess)
{
    var serializer = serializerBuilder.Value;
    
    // Serialize, compress, and encrypt an object
    var data = new SensitiveData { ApiKey = "secret-api-key", Password = "secure-password" };
    var serializeResult = await serializer.SerializeAsync(data);
    
    if (serializeResult.IsSuccess)
    {
        // Store the encrypted data securely
        byte[] secureData = serializeResult.Value;
        
        // Decrypt, decompress, and deserialize the data
        var deserializeResult = await serializer.DeserializeAsync<SensitiveData>(secureData);
        
        if (deserializeResult.IsSuccess)
        {
            Console.WriteLine("Successfully retrieved sensitive data.");
        }
    }
}
```

### Using MessagePack

```csharp
// Create a serializer with MessagePack
var serializerBuilder = new SerializationBuilder()
    .WithSerializer<MessagePackSerializer>()
    .WithDefaultMessagePackSerializerOptions()
    .Build();

if (serializerBuilder.IsSuccess)
{
    var serializer = serializerBuilder.Value;
    
    // Serialize an object with MessagePack
    var data = new DataPoint { Timestamp = DateTime.UtcNow, Value = 42.5 };
    var serializeResult = await serializer.SerializeAsync(data);
    
    if (serializeResult.IsSuccess)
    {
        // MessagePack serialized data is typically smaller than JSON
        byte[] compactData = serializeResult.Value;
        Console.WriteLine($"Serialized size: {compactData.Length} bytes");
        
        // Deserialize the data
        var deserializeResult = await serializer.DeserializeAsync<DataPoint>(compactData);
        
        if (deserializeResult.IsSuccess)
        {
            Console.WriteLine($"Deserialized data point: {deserializeResult.Value.Timestamp}, {deserializeResult.Value.Value}");
        }
    }
}
```

### Working with Streams

```csharp
// Create a stream serializer
var serializerBuilder = new SerializationBuilder()
    .WithStreamSerializer<JsonStreamSerializer>()
    .WithDefaultJsonSerializerOptions()
    .Build();

if (serializerBuilder.IsSuccess)
{
    var serializer = serializerBuilder.Value;
    
    // Work with large data directly through streams
    using var fileStream = File.OpenRead("large-data.json");
    var deserializeResult = await serializer.DeserializeAsync<LargeDataSet>(fileStream);
    
    if (deserializeResult.IsSuccess)
    {
        Console.WriteLine($"Successfully deserialized {deserializeResult.Value.Items.Count} items.");
    }
}
```

## Architecture

The library follows a layered architecture:

1. **Interfaces**: Core contracts for serialization components
2. **Providers**: Implementations for different compression, encoding, and encryption strategies
3. **Serializers**: Implementations for different serialization formats
4. **Builders**: Fluent builders for constructing and configuring serializers
5. **Error Handling**: Domain-specific error types with the Result pattern

## Error Handling

The library uses a Result pattern to handle errors in a strongly-typed way. All operations return a `Result<T, SerializationError>` object that either contains the successful value or detailed error information.

```csharp
var result = await serializer.DeserializeAsync<MyData>(byteArray);

if (result.IsSuccess)
{
    // Work with result.Value
    var data = result.Value;
}
else
{
    // Handle the error
    Console.WriteLine($"Error: {result.Error.Message}");
    
    // You can also match on different error cases
    result.Match(
        value => Console.WriteLine($"Success: {value}"),
        error => Console.WriteLine($"Error: {error.Message}, Operation: {error.Operation}")
    );
}
```

## Performance Optimizations

The library includes several optimizations for performance:

- **Buffer Pooling**: Uses `ArrayPool<T>` for efficient buffer management
- **RecyclableMemoryStream**: Reduces GC pressure by recycling memory streams
- **Span/Memory**: Uses modern .NET APIs for zero-allocation operations where possible
- **Caching**: Optional caching for frequently serialized objects
- **Conditional Processing**: Skip compression/encryption for small objects when appropriate
- **Optimized Algorithms**: Custom implementations for encoding/hashing with performance in mind

## API Overview

### Key Components

#### Interfaces

- `ISerializer`: Main interface for serialization operations
- `IStreamSerializer`: Interface for stream-based serialization
- `ICompressor`: Interface for compression operations
- `IEncoder`: Interface for encoding operations
- `IEncryptor`: Interface for encryption operations

#### Builders

- `SerializationBuilder`: Creates and configures serializers
- `SerializationConfig`: Holds configuration for serialization components

#### Serializers

- `JsonSerializer`: System.Text.Json-based serializer
- `MessagePackSerializer`: MessagePack-based serializer
- `CombinedSerializer`: Smart serializer that chooses the best approach based on data
- Decorator serializers: `CompressedSerializer`, `EncodedSerializer`, `EncryptedSerializer`

#### Providers

- Compression: `GZipCompressionProvider`, `DeflateCompressionProvider`
- Encoding: `Base64EncodingProvider`, `HexEncodingProvider`
- Encryption: `AESGCMEncryptionProvider`, `AESCNGEncryptionProvider`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
