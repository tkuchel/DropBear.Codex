# DropBear Codex Core

![Nuget](https://img.shields.io/nuget/v/DropBear.Codex.Core?style=flat-square)
![Build](https://img.shields.io/github/actions/workflow/status/tkuchel/DropBear.Codex/core.yml?branch=main&style=flat-square)
![License](https://img.shields.io/github/license/tkuchel/DropBear.Codex?style=flat-square)

## Overview

**DropBear Codex Core** is a foundational .NET library that provides a comprehensive result handling system with strong type safety and functional programming patterns. The library offers a robust set of tools for managing operation outcomes, error handling, and data validation in a type-safe and composable manner.

## Features

### Result Types
- **Base Result Types**: Type-safe result handling with `Result<TError>` and `Result<T, TError>`
- **Specialized Error Types**: Built-in error types for common scenarios (Validation, Database, Timeout)
- **Backwards Compatibility**: Legacy support through `Result` and `Result<T>` classes
- **Payload Handling**: Specialized `ResultWithPayload<T>` for compressed and validated data transfer

### Functional Programming Support
- **Monadic Operations**: `Map`, `Bind`, `Apply` for composable operations
- **LINQ Integration**: Full LINQ support for result types
- **Pattern Matching**: Comprehensive pattern matching capabilities
- **Lazy Evaluation**: Support for deferred computation

### Error Handling
- **Type-safe Errors**: Strong typing for error scenarios
- **Error Composition**: Ability to combine and transform errors
- **Validation Chains**: Fluent validation API
- **Async Support**: Full async/await integration

### Extension Methods
- **LINQ Extensions**: Query and transform results
- **Validation Extensions**: Fluent validation API
- **Async Extensions**: Comprehensive async operation support
- **Combination Extensions**: Compose and combine results

## Getting Started

### Installation

```sh
dotnet add package DropBear.Codex.Core
```

### Basic Usage

#### Type-safe Error Handling
```csharp
public record ValidationError(string Field, string Message) : ResultError(Message);

public Result<User, ValidationError> ValidateUser(UserInput input)
{
    return input.Username.Length < 3 
        ? Result<User, ValidationError>.Failure(new ValidationError("username", "Too short"))
        : Result<User, ValidationError>.Success(new User(input.Username));
}
```

#### Chaining Operations
```csharp
var result = await GetUserAsync()
    .BindAsync(ValidateUserAsync)
    .MapAsync(SaveUserAsync)
    .MapError(err => new DatabaseError("SaveUser", err.Message));

result.Match(
    user => Console.WriteLine($"Saved user: {user.Id}"),
    error => Console.WriteLine($"Failed: {error.Message}")
);
```

#### Validation Chains
```csharp
var result = await userInput
    .ValidateAsync(
        async u => await ValidateUsername(u),
        async u => await ValidateEmail(u),
        async u => await ValidatePassword(u)
    );
```

#### Working with Collections
```csharp
var results = await users
    .TraverseAsync(async user => await ValidateUserAsync(user))
    .MapAsync(validUsers => SaveUsersAsync(validUsers));
```

#### Payload Handling
```csharp
var payloadResult = await ResultWithPayload<UserData>.SuccessAsync(userData);
if (payloadResult.IsValid)
{
    var decompressed = await payloadResult
        .DecompressAndDeserialize<UserData>()
        .BindAsync(SaveUserDataAsync);
}
```

## Advanced Features

### Custom Error Types
```csharp
public record DatabaseError : ResultError
{
    public DatabaseError(string operation, string message) 
        : base($"{operation} failed: {message}") 
    {
        Operation = operation;
    }

    public string Operation { get; }
}
```

### Async Operations with Timeout
```csharp
var result = await operation
    .RetryAsync(maxAttempts: 3, delay: TimeSpan.FromSeconds(1))
    .TimeoutAfter(TimeSpan.FromSeconds(5));
```

### Result Combination
```csharp
var combined = await Task.WhenAll(
    ValidateUserAsync(user),
    ValidateRolesAsync(roles),
    ValidatePermissionsAsync(permissions)
);
```

## Best Practices

1. **Use Type-safe Errors**: Create specific error types for different scenarios
2. **Leverage Async Operations**: Use async methods where appropriate
3. **Chain Operations**: Use functional composition instead of nested if statements
4. **Handle All States**: Always handle both success and failure cases
5. **Validate Early**: Use the validation extensions for input validation

## Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

© 2024 Terrence Kuchel (DropBear)
