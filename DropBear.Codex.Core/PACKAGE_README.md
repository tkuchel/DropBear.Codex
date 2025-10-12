# DropBear.Codex.Core

[![NuGet](https://img.shields.io/nuget/v/DropBear.Codex.Core.svg)](https://www.nuget.org/packages/DropBear.Codex.Core/)
[![Downloads](https://img.shields.io/nuget/dt/DropBear.Codex.Core.svg)](https://www.nuget.org/packages/DropBear.Codex.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Core library providing **Result pattern**, **functional extensions**, and **telemetry infrastructure** for .NET 9+ applications.

## 🚀 Quick Start

### Installation

```bash
dotnet add package DropBear.Codex.Core
```

### Basic Usage

```csharp
using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Errors;

// Create a result-returning method
public Result<int, ValidationError> Divide(int numerator, int denominator)
{
    if (denominator == 0)
    {
        return Result<int, ValidationError>.Failure(
            new ValidationError("Cannot divide by zero"));
    }
    
    return Result<int, ValidationError>.Success(numerator / denominator);
}

// Use pattern matching
var result = Divide(10, 2);
var message = result.Match(
    success: value => $"Result: {value}",
    failure: error => $"Error: {error.Message}"
);

// Chain operations
var doubled = result.Map(x => x * 2);
var combined = result.Bind(x => Divide(x, 3));
```

## ✨ Features

### Result Pattern
Type-safe error handling without exceptions:
```csharp
Result<User, ValidationError> GetUser(int id)
{
    if (id <= 0)
        return Result<User, ValidationError>.Failure(
            new ValidationError("Invalid ID"));
    
    var user = _repository.Find(id);
    return user != null
        ? Result<User, ValidationError>.Success(user)
        : Result<User, ValidationError>.Failure(
            new ValidationError("User not found"));
}
```

### Functional Extensions
**Map**, **Bind**, **Match** and more:
```csharp
var result = await GetUserAsync(userId)
    .MapAsync(user => user.Email)
    .BindAsync(email => SendEmailAsync(email))
    .TapAsync(async () => await LogSuccessAsync())
    .OnFailureAsync(async error => await LogErrorAsync(error));
```

### Telemetry Integration
Built-in OpenTelemetry support:
```csharp
// Configure telemetry at startup
TelemetryProvider.Configure(options =>
{
    options.Mode = TelemetryMode.BackgroundChannel;
    options.ChannelCapacity = 10000;
});

// Automatic telemetry tracking
var result = Result<int, ValidationError>.Success(42);
// Telemetry automatically recorded!
```

### Async/Await Support
First-class async operations:
```csharp
public async Task<Result<Data, ErrorType>> ProcessAsync(
    CancellationToken cancellationToken = default)
{
    return await GetDataAsync(cancellationToken)
        .MapAsync(async (data, ct) => await TransformAsync(data, ct), cancellationToken)
        .BindAsync(async (data, ct) => await ValidateAsync(data, ct), cancellationToken);
}
```

### Envelope Pattern
Message wrapping with metadata:
```csharp
var envelope = new EnvelopeBuilder<MyData>()
    .WithPayload(myData)
    .AddHeader("version", "1.0")
    .AddHeader("source", "api")
    .Build();

// Serialize and deserialize
var json = await envelopeSerializer.SerializeAsync(envelope);
var restored = await envelopeSerializer.DeserializeAsync(json);
```

## 📖 Documentation

- [Full Documentation](https://github.com/tkuchel/DropBear.Codex)
- [API Reference](https://github.com/tkuchel/DropBear.Codex/wiki/API-Reference)
- [Examples & Recipes](https://github.com/tkuchel/DropBear.Codex/wiki/Examples)
- [Migration Guide](https://github.com/tkuchel/DropBear.Codex/wiki/Migration-Guide)

## 🎯 Key Benefits

- ✅ **Type-Safe** - Compile-time error handling
- ✅ **Railway Oriented** - Clean error propagation
- ✅ **High Performance** - .NET 9 optimized with minimal allocations
- ✅ **Observable** - OpenTelemetry integration out of the box
- ✅ **Testable** - Pure functions, easy to mock
- ✅ **Well-Documented** - Comprehensive XML docs

## 🔧 Requirements

- .NET 9.0 or later
- C# 12 or later

## 📦 Related Packages

- **DropBear.Codex.Blazor** - Blazor components and utilities
- **DropBear.Codex.Hashing** - Cryptographic hashing utilities
- **DropBear.Codex.Serialization** - Advanced serialization

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/tkuchel/DropBear.Codex/blob/master/LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](https://github.com/tkuchel/DropBear.Codex/blob/master/CONTRIBUTING.md) first.

## 💬 Support

- 📧 Email: [your-email@example.com]
- 🐛 Issues: [GitHub Issues](https://github.com/tkuchel/DropBear.Codex/issues)
- 💡 Discussions: [GitHub Discussions](https://github.com/tkuchel/DropBear.Codex/discussions)

---

Made with ❤️ by [Terrence Kuchel](https://github.com/tkuchel)