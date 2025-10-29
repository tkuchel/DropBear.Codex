# DropBear Codex

**DropBear Codex** is a collection of modular C# libraries built with .NET 9, designed to provide robust, reusable components for enterprise-grade applications. The libraries follow Railway-Oriented Programming principles with a comprehensive Result pattern for error handling. All libraries are published as NuGet packages for easy integration.

[![CI (.NET 9)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/ci.yml/badge.svg)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/ci.yml)
[![CodeQL](https://github.com/tkuchel/DropBear.Codex/actions/workflows/codeql.yml/badge.svg)](https://github.com/tkuchel/DropBear.Codex/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Key Features

- 🎯 **Railway-Oriented Programming**: Comprehensive Result pattern for type-safe error handling
- 🔄 **Workflow Engine**: DAG-based workflows with compensation (Saga pattern)
- 🔐 **Security First**: Built-in hashing, encryption, and secure serialization
- 📦 **Modular Design**: Use only what you need - each library is independent
- ✅ **Production Ready**: Extensive test coverage, code analysis, and quality gates
- 🚀 **Modern .NET 9**: Built with latest C# 12+ features and performance optimizations
- 📊 **Observability**: OpenTelemetry integration for distributed tracing

## Table of Contents
- [Projects](#projects)
- [Getting Started](#getting-started)
- [Installation](#installation)
- [Usage](#usage)
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
- **DropBear.Codex.StateManagement**: State machine implementation using the Stateless library with snapshot management.
- **DropBear.Codex.Tasks**: Task/operation managers with retry, fallback, and resilience support.

### UI & Notifications
- **DropBear.Codex.Blazor**: Custom Blazor component library for interactive web applications.
- **DropBear.Codex.Notifications**: Notification infrastructure with multiple channels and delivery strategies.

## Getting Started

### Prerequisites
- **.NET 9 SDK** or later
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

**State Machine:**
```csharp
var builder = new StateMachineBuilder<State, Trigger>(State.Initial, logger);
builder
    .ConfigureState(State.Initial)
        .Permit(Trigger.Start, State.Running)
    .Build();

await stateMachine.FireAsync(Trigger.Start);
```

For detailed examples and advanced usage, see the [CLAUDE.md](CLAUDE.md) file and individual project documentation.

## Contributing
Contributions are welcome! If you’d like to contribute, please follow these steps:

1. Fork the repository.
2. Create a new branch (`git checkout -b feature/your-feature`).
3. Make your changes and commit them (`git commit -m 'Add your feature'`).
4. Push to the branch (`git push origin feature/your-feature`).
5. Open a Pull Request.

Please make sure your code adheres to the project's coding standards and includes relevant tests.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
