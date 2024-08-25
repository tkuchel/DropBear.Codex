# DropBear Codex Core

![Nuget](https://img.shields.io/nuget/v/DropBear.Codex.Core?style=flat-square)
![Build](https://img.shields.io/github/actions/workflow/status/tkuchel/DropBear.Codex/core.yml?branch=main&style=flat-square)
![License](https://img.shields.io/github/license/tkuchel/DropBear.Codex?style=flat-square)

## Overview

**DropBear Codex Core** is a foundational .NET library that provides a set of core utilities, types, and base classes for handling results and operations within the DropBear Codex ecosystem. This library is designed to be lightweight, efficient, and easy to integrate into various applications.

The core components of this library include result types for encapsulating the outcome of operations, ensuring that success, failure, and other states are handled consistently across your applications.

## Features

- **Result Handling**: Standardized classes (`Result`, `Result<T>`, `Result<TSuccess, TFailure>`, and `ResultWithPayload<T>`) to represent the outcome of operations, including success, failure, warnings, partial success, and more.
- **Payload Management**: Support for compressing, hashing, and validating payloads in operations, making it easy to handle large data sets efficiently.
- **Extensible**: The library is designed with extensibility in mind, allowing you to build on top of the provided result types and adapt them to your specific needs.
- **Robust Error Handling**: Comprehensive error handling mechanisms to ensure that exceptions are captured, logged, and managed appropriately.

## Getting Started

### Installation

You can install the `DropBear.Codex.Core` package via NuGet:

```sh
dotnet add package DropBear.Codex.Core
```

Or via the NuGet Package Manager in Visual Studio:

```sh
Install-Package DropBear.Codex.Core
```

### Usage

#### Basic Result Handling

The core of the library is the `Result` class, which encapsulates the outcome of an operation:

```csharp
using DropBear.Codex.Core;

var result = Result.Success();
if (result.IsSuccess)
{
    // Handle success
}
else
{
    // Handle failure
}
```

#### Working with `Result<T>`

For operations that return a value, use `Result<T>`:

```csharp
var result = Result<int>.Success(42);
int value = result.ValueOrDefault();

if (result.IsSuccess)
{
    Console.WriteLine($"Operation succeeded with value: {value}");
}
else
{
    Console.WriteLine("Operation failed.");
}
```

#### Handling Success and Failure in Operations

```csharp
var result = Result<int>.Try(() => SomeOperationThatMayFail());

result.OnSuccess(() =>
{
    // Handle success
})
.OnFailure((errorMessage, exception) =>
{
    // Handle failure, log errorMessage and exception
});
```

#### Result with Payload

For operations involving payloads:

```csharp
var payloadResult = ResultWithPayload<string>.SuccessWithPayload("Hello, World!");

if (payloadResult.IsValid)
{
    var decompressedResult = payloadResult.DecompressAndDeserialize();
    Console.WriteLine(decompressedResult.ValueOrDefault());
}
else
{
    Console.WriteLine("Payload validation failed.");
}
```

## Contributing

Contributions are welcome! Please read the [CONTRIBUTING.md](https://github.com/tkuchel/DropBear.Codex/blob/main/CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/tkuchel/DropBear.Codex/blob/main/LICENSE) file for details.

## Acknowledgements

Special thanks to the .NET community for their continuous support and contributions to the ecosystem.

## Contact

For any questions or suggestions, feel free to open an issue on GitHub or contact the project maintainer:

- [GitHub Issues](https://github.com/tkuchel/DropBear.Codex/issues)
- [Email](mailto:your.emailexample.com)

---

© 2024 Terrence Kuchel (DropBear)
