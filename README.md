# DropBear Codex

**DropBear Codex** is a collection of modular C# libraries built with .NET 10, designed to provide robust, reusable components for enterprise-grade applications. The libraries follow Railway-Oriented Programming principles with a comprehensive Result pattern for error handling. All libraries are published as NuGet packages for easy integration.

[![CI (.NET 10)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/ci.yml/badge.svg)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/ci.yml)
[![CodeQL](https://github.com/tkuchel/DropBear.Codex/actions/workflows/codeql.yml/badge.svg)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Key Features

- 🎯 **Railway-Oriented Programming**: Comprehensive Result pattern for type-safe error handling
- 🔄 **Workflow Engine**: DAG-based workflows with compensation (Saga pattern)
- 🔐 **Security First**: Built-in hashing, encryption, and secure serialization
- 📦 **Modular Design**: Use only what you need - each library is independent
- ✅ **Production Ready**: Extensive test coverage, code analysis, and quality gates
- 🚀 **Modern .NET 10**: Built with current .NET and C# features plus performance-oriented defaults
- 📊 **Observability**: OpenTelemetry integration for distributed tracing
- 🛡️ **Security Hardened**: Recent audit-driven fixes across crypto, serialization, workflow persistence, Blazor rendering, and notification ownership

## Table of Contents
- [Projects](#projects)
- [Getting Started](#getting-started)
- [Installation](#installation)
- [Usage](#usage)
- [Security Notes](#security-notes)
- [Contributing](#contributing)
- [License](#license)

## Projects
The solution includes the following libraries:

### Core Infrastructure
- **DropBear.Codex.Core**: Foundation library providing the Result pattern, error handling, and telemetry infrastructure. Features type-safe error handling, functional extensions, and OpenTelemetry integration.
- **DropBear.Codex.Utilities**: Extension methods and helper classes for common operations.

### Data Management
- **DropBear.Codex.Serialization**: Serialization wrappers around JSON, MessagePack, and encrypted serialization.
- **DropBear.Codex.Hashing**: Various hashing implementations including Argon2, Blake2, Blake3, xxHash, and SHA-family algorithms.
- **DropBear.Codex.Files**: Custom file format with integrated serialization, compression, and cryptographic verification.

### Workflow & State Management
- **DropBear.Codex.Workflow**: DAG-based workflow engine with compensation support (Saga pattern) for complex multi-step processes.
- **DropBear.Codex.Tasks**: Task/operation managers with retry, fallback, and resilience support.

### UI & Notifications
- **DropBear.Codex.Blazor**: Custom Blazor component library for interactive web applications.
- **DropBear.Codex.Notifications**: Notification infrastructure with multiple channels and delivery strategies.

### Tooling
- **DropBear.Codex.Benchmarks**: BenchmarkDotNet suite for tracking hot-path performance and regression risk across the libraries.

## Getting Started

### Prerequisites
- **.NET 10 SDK** or later
- A .NET development environment (JetBrains Rider, Visual Studio 2022, or VS Code)

### Development Setup

1. **Clone the repository**:
```bash
git clone https://github.com/tkuchel/dropbear-codex.git
cd dropbear-codex
```

2. **Restore packages**:
```bash
dotnet restore
```

3. **Build the solution**:
```bash
dotnet build
```

4. **Run tests** (optional):
```bash
dotnet test
```

## Installation
The libraries are available as NuGet packages. To install a package, use the following command:

```bash
dotnet add package DropBear.Codex.<LibraryName>
```

Replace `<LibraryName>` with the specific library you wish to install.

## Usage
Each library is designed to be modular and can be used independently. Detailed usage instructions can be found in the individual library's README files.

### Railway-Oriented Programming with Result Pattern

The core philosophy of DropBear.Codex is Railway-Oriented Programming using the Result pattern for type-safe error handling:

```csharp
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.ReturnTypes;

// Example 1: Simple operation with error handling
public Result<User, SimpleError> GetUser(int userId)
{
    var user = _repository.Find(userId);
    return user is not null
        ? Result<User, SimpleError>.Success(user)
        : Result<User, SimpleError>.Failure(new SimpleError("User not found"));
}

// Example 2: Chaining operations
var result = GetUser(123)
    .Map(user => user.Email)
    .Match(
        onSuccess: email => Console.WriteLine($"User email: {email}"),
        onFailure: error => Console.WriteLine($"Error: {error.Message}")
    );

// Example 3: Async operations
var userResult = await GetUserAsync(userId)
    .BindAsync(async user => await EnrichUserDataAsync(user))
    .MapAsync(async user => user.ToDto());

if (userResult.IsSuccess)
{
    ProcessUser(userResult.Value);
}
```

### Quick Start Examples

**Workflow Engine:**
```csharp
var builder = new WorkflowBuilder<MyContext>("workflow-1", "My Workflow");
builder
    .StartWith<ValidationStep>()
    .Then<ProcessingStep>()
    .Build();

var result = await engine.ExecuteAsync(workflow, context);
```

For detailed examples and advanced usage, see the [CLAUDE.md](CLAUDE.md) file and individual project documentation.

## Security Notes

The library has recently gone through a broad security review and remediation pass. Key hardening areas include:

- Secret-backed anti-forgery token validation and tighter CSP defaults in `DropBear.Codex.Blazor`
- Removal of unsafe type/provider resolution from persisted and file metadata paths
- Authenticated encryption for protected serialization payloads
- Correct envelope sealing/signature invalidation on mutation
- Root-scoped local storage and enforced buffered-read / serialization memory limits
- Notification ownership enforcement to prevent cross-user access by identifier
- Fail-closed handling of untrusted HTML and stricter SVG custom icon validation
- Safer default MessagePack and JSON serializer helper configurations

Some APIs are intentionally explicit sharp edges, such as trusted HTML rendering helpers. When a helper says content must already be trusted, it should be treated as an application-level responsibility rather than a built-in safety boundary.

## Contributing

Contributions are welcome! Please see our [Contributing Guidelines](CONTRIBUTING.md) for detailed information on:

- Development setup and workflow
- Code style and standards
- Testing requirements
- Pull request process

For maintainers creating releases, see [RELEASE.md](RELEASE.md) for the release process and tagging guidelines.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
