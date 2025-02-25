# DropBear.Codex.Files

A high-performance, memory-optimized file management library for .NET 8 applications that supports local and cloud storage operations with robust error handling.

## Overview

DropBear.Codex.Files is a comprehensive file handling library designed to provide a consistent, strongly-typed approach to file operations. It features robust error handling using a Result pattern, memory optimization, and support for both local file systems and Azure Blob Storage.

The library supports serialization, compression, and encryption of file content, making it suitable for secure data storage and transfer scenarios.

## Features

- **Robust Error Handling**: Uses a strongly-typed Result pattern for predictable error handling
- **Memory Optimization**: Implements buffer pooling and recyclable memory streams to minimize GC pressure
- **Multiple Storage Providers**:
  - Local file system storage with optimized I/O operations
  - Azure Blob Storage support with configurable connection options
- **Content Management**:
  - Support for serialization with customizable options
  - Optional compression of file content
  - Optional encryption of sensitive data
- **Builder Pattern API**: Fluent, easy-to-use API for constructing files and configuring options
- **Async/Await First**: Fully asynchronous API with proper cancellation token support
- **Performance Optimized**: Uses .NET 8 features for maximum performance

## Installation

```
dotnet add package DropBear.Codex.Files
```

## Dependencies

- .NET 8.0 or higher
- FluentStorage (for Azure Blob Storage support)
- Microsoft.IO.RecyclableMemoryStream
- Serilog
- DropBear.Codex.Core
- DropBear.Codex.Serialization
- DropBear.Codex.Hashing

## Basic Usage

### Creating and Writing a File

```csharp
// Create a file manager with local storage
var fileManagerBuilder = new FileManagerBuilder()
    .UseLocalStorage("C:/MyFiles")
    .Build();

if (fileManagerBuilder.IsSuccess)
{
    var fileManager = fileManagerBuilder.Value;
    
    // Create a content container
    var containerBuilder = new ContentContainerBuilder()
        .WithData("Hello, world!")
        .WithContentType("text/plain");
    
    var containerResult = await containerBuilder.BuildAsync();
    
    if (containerResult.IsSuccess)
    {
        // Create a DropBearFile with the container
        var fileBuilder = new DropBearFileBuilder()
            .WithFileName("helloworld.txt")
            .WithVersion("1.0.0", DateTimeOffset.UtcNow)
            .AddMetadata("Author", "DropBear")
            .AddContentContainer(containerResult.Value);
        
        var fileResult = fileBuilder.Build();
        
        if (fileResult.IsSuccess)
        {
            // Write the file
            var writeResult = await fileManager.WriteToFileAsync(
                fileResult.Value, 
                "helloworld.dbf");
                
            if (writeResult.IsSuccess)
            {
                Console.WriteLine("File successfully written!");
            }
        }
    }
}
```

### Reading a File

```csharp
var fileManagerBuilder = new FileManagerBuilder()
    .UseLocalStorage("C:/MyFiles")
    .Build();

if (fileManagerBuilder.IsSuccess)
{
    var fileManager = fileManagerBuilder.Value;
    
    // Read the file
    var readResult = await fileManager.ReadFromFileAsync<DropBearFile>("helloworld.dbf");
    
    if (readResult.IsSuccess)
    {
        var file = readResult.Value;
        
        // Get the content container
        var container = fileManager.GetContainerByContentType(file, "text/plain");
        
        if (container != null)
        {
            // Get the data from the container
            var dataResult = await container.GetDataAsync<string>();
            
            if (dataResult.IsSuccess)
            {
                Console.WriteLine($"File content: {dataResult.Value}");
            }
        }
    }
}
```

### Using Azure Blob Storage

```csharp
var fileManagerBuilder = new FileManagerBuilder()
    .UseBlobStorage(
        "myStorageAccount", 
        "myAccessKey", 
        "myContainer")
    .Build();

if (fileManagerBuilder.IsSuccess)
{
    var fileManager = fileManagerBuilder.Value;
    
    // Read from blob storage
    var readResult = await fileManager.ReadFromFileAsync<DropBearFile>("myBlob.dbf");
    
    // Rest of the code is the same as with local storage...
}
```

## Architecture

The library follows a layered architecture:

1. **Models**: Core data structures (`DropBearFile`, `ContentContainer`, `FileVersion`)
2. **Builders**: Fluent builders for constructing objects (`DropBearFileBuilder`, `ContentContainerBuilder`)
3. **Storage Managers**: Implementations for different storage backends (`LocalStorageManager`, `BlobStorageManager`)
4. **File Manager**: High-level API for working with files (`FileManager`)
5. **Error Handling**: Domain-specific error types (`FilesError`, `ContentContainerError`, etc.)

## Error Handling

The library uses a Result pattern to handle errors in a strongly-typed way. All operations return a `Result<T, TError>` object that either contains the successful value or detailed error information.

```csharp
var result = await fileManager.ReadFromFileAsync<DropBearFile>("myFile.dbf");

if (result.IsSuccess)
{
    // Work with result.Value
    var file = result.Value;
}
else
{
    // Handle the error
    Console.WriteLine($"Error: {result.Error.Message}");
    
    // You can also match on different error cases
    result.Match(
        value => Console.WriteLine($"Success: {value.FileName}"),
        error => Console.WriteLine($"Error: {error.Message}")
    );
}
```

## Performance Optimizations

The library includes several optimizations for performance:

- **Memory Pooling**: Uses `ArrayPool<T>` for efficient buffer management
- **RecyclableMemoryStream**: Reduces GC pressure by recycling memory streams
- **Asynchronous I/O**: Uses non-blocking I/O operations with configurable buffer sizes
- **Cancellation Support**: All async operations support cancellation tokens
- **Type Caching**: Caches Type lookups in serialization/deserialization
- **Frozen Collections**: Uses immutable collections for read-only data

## API Overview

### Key Classes

#### Models

- `DropBearFile`: The main file container that holds metadata and content containers
- `ContentContainer`: Holds a piece of data with flags indicating how to process it
- `FileVersion`: Represents version information about a file

#### Builders

- `FileManagerBuilder`: Creates and configures a FileManager instance
- `DropBearFileBuilder`: Creates and configures a DropBearFile instance
- `ContentContainerBuilder`: Creates and configures a ContentContainer instance

#### Services

- `FileManager`: Main service for file operations
- `LocalStorageManager`: Manages local file system operations
- `BlobStorageManager`: Manages Azure Blob Storage operations

#### Interfaces

- `IStorageManager`: Defines the contract for storage providers

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
