# DropBear Codex Core

![Nuget](https://img.shields.io/nuget/v/DropBear.Codex.Core?style=flat-square)
![Build](https://img.shields.io/github/actions/workflow/status/tkuchel/DropBear.Codex/core.yml?branch=main&style=flat-square)
![License](https://img.shields.io/github/license/tkuchel/DropBear.Codex?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)

## Overview

**DropBear Codex Core** is a modern .NET library providing a comprehensive and type-safe Result handling system. Built with functional programming principles, it offers robust tools for managing operation outcomes, error handling, and validation in a composable and maintainable way.

## Key Features

### Result Types
- 🛡️ **Type-safe Results**: Generic `Result<TError>` and `Result<T, TError>` with strong typing
- 🔄 **Specialized Results**: `ResultWithPayload<T>` for compression and validation
- 📦 **Comprehensive Errors**: Built-in `ValidationError`, `DatabaseError`, and `TimeoutError`
- 🔗 **Backwards Compatibility**: Legacy support via `Result` and `Result<T>`

### Functional Operations
- 🔄 **Monadic Methods**
    - `Map` for transforming success values
    - `Bind` for chaining operations
    - `MapError` for error transformation
- 🔍 **LINQ Integration**
    - Full query syntax support
    - Parallel processing capabilities
    - Collection operations
- 🎯 **Pattern Matching**
    - Comprehensive `Match` methods
    - State-specific handling
    - Custom matching patterns

### Error Handling & Validation
- ✅ **Validation Pipeline**
    - Fluent validation API
    - Async validation support
    - Composable validators
- 🔄 **Retry Mechanisms**
    - Configurable retry policies
    - Exponential backoff
    - Timeout handling
- 📊 **Error Composition**
    - Error aggregation
    - Error transformation
    - Custom error types

## Installation

```bash
# Package Manager
Install-Package DropBear.Codex.Core

# .NET CLI
dotnet add package DropBear.Codex.Core

# PackageReference
<PackageReference Include="DropBear.Codex.Core" Version="1.0.0" />
```

## Usage Examples

### Basic Result Handling
```csharp
public Result<User, ValidationError> ValidateUser(UserInput input)
{
    return input.Email.Contains("@")
        ? Result<User, ValidationError>.Success(new User(input.Email))
        : Result<User, ValidationError>.Failure(
            new ValidationError("email", "Invalid email format"));
}
```

### Async Operations
```csharp
public async Task<Result<User, DatabaseError>> CreateUserAsync(UserInput input)
{
    // Validate and create user
    var result = await ValidateUser(input)
        .BindAsync(SaveUserAsync)
        .OnSuccess(user => Logger.Info("User created: {Id}", user.Id))
        .MapError(error => new DatabaseError(
            "CreateUser",
            error.Message,
            error.Code));

    return result;
}
```

### Validation Chains
```csharp
public async Task<Result<UserProfile, ValidationError>> ValidateProfile(UserProfileInput input)
{
    return await input.Validate(
        profile => ValidateBasicInfo(profile),
        profile => ValidateContactInfo(profile),
        async profile => await ValidateUniqueEmail(profile)
    );
}
```

### Working with Collections
```csharp
public async Task<Result<IReadOnlyList<User>, ValidationError>> ProcessUsers(IEnumerable<UserInput> inputs)
{
    return await inputs
        .Select(ValidateUser)           // Validate each user
        .Traverse()                     // Combine results
        .BindAsync(SaveUsersAsync)      // Save if all valid
        .OnSuccess(users => 
            Logger.Info("Processed {Count} users", users.Count));
}
```

### Payload Handling
```csharp
public async Task<Result<UserData, PayloadError>> ProcessUserData(UserData data)
{
    var result = await ResultWithPayload<UserData>
        .CreateAsync(data)
        .BindAsync(async payload => 
        {
            if (!payload.IsValid)
                return Result<UserData, PayloadError>.Failure(
                    new PayloadError("Invalid payload hash"));

            return await payload
                .DecompressAndDeserializeAsync<UserData>()
                .BindAsync(SaveUserDataAsync);
        });

    return result;
}
```

## Best Practices

### Error Handling
- Create specific error types for different scenarios
- Use `Match` for exhaustive pattern matching
- Handle both success and failure cases explicitly
- Leverage async methods where appropriate

### Validation
- Validate early in the operation chain
- Combine multiple validations using `Validate`
- Use field-specific validation errors
- Consider async validation when needed

### Performance
- Use `ValueTask` for better async performance
- Leverage parallel processing for collections
- Consider lazy evaluation for expensive operations
- Use appropriate buffer sizes for payload handling

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:
- Code of Conduct
- Development workflow
- Pull request process
- Coding standards

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- 📚 [Documentation](https://docs.dropbear.codex.core)
- 💬 [Discussions](https://github.com/tkuchel/DropBear.Codex/discussions)
- 🐛 [Issue Tracker](https://github.com/tkuchel/DropBear.Codex/issues)

---

© 2024 Terrence Kuchel (DropBear). All rights reserved.
