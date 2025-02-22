# DropBear.Codex Result Types

A comprehensive set of result types for handling success, failure, and validation scenarios in a functional way.

## Table of Contents

- [Overview](#overview)
- [Result Types](#result-types)
  - [Base Result Types](#base-result-types)
  - [Specialized Result Types](#specialized-result-types)
  - [Legacy Result Types](#legacy-result-types)
- [Usage Examples](#usage-examples)
- [Best Practices](#best-practices)
- [Advanced Scenarios](#advanced-scenarios)

## Overview

The Result types provide a robust way to handle operation outcomes without relying on exceptions for control flow. They offer:

- Type-safe error handling
- Rich error context
- Functional programming patterns
- Async operation support
- Object pooling for performance
- Comprehensive debugging support

## Result Types

### Base Result Types

#### `Result<TError>` - Base Generic Error Result

```csharp
// Define a custom error type
public record CustomError : ResultError
{
    public CustomError(string message) : base(message) { }
    public string ErrorCode { get; init; }
}

// Using the base result type
public Result<CustomError> ProcessOperation()
{
    try
    {
        // Operation logic
        return Result<CustomError>.Success();
    }
    catch (Exception ex)
    {
        return Result<CustomError>.Failure(
            new CustomError("Operation failed")
            {
                ErrorCode = "ERR001"
            }, 
            ex);
    }
}
```

#### `Result<T, TError>` - Generic Value and Error Result

```csharp
// Processing with value and error handling
public Result<int, CustomError> Calculate(int value)
{
    if (value < 0)
    {
        return Result<int, CustomError>.Failure(
            new CustomError("Value must be positive")
            {
                ErrorCode = "VAL001"
            });
    }

    return Result<int, CustomError>.Success(value * 2);
}

// Using functional methods
var result = Calculate(10)
    .Map(x => x.ToString())  // Transform success value
    .Bind(str => ValidateString(str))  // Chain operations
    .MapError(err => new DifferentError(err.Message));  // Transform error type
```

### Specialized Result Types

#### `ValidationResult` - For Validation Scenarios

```csharp
public ValidationResult ValidateUser(User user)
{
    // Simple validation
    if (string.IsNullOrEmpty(user.Name))
    {
        return ValidationResult.PropertyFailed(
            "Name", 
            "Name is required",
            user.Name);
    }

    // Complex validation
    var emailResult = await ValidateEmailAsync(user.Email);
    if (!emailResult.IsValid)
    {
        return emailResult;
    }

    // Rule-based validation
    if (user.Age < 18)
    {
        return ValidationResult.RuleFailed(
            "AgeRequirement",
            "User must be 18 or older",
            user.Age);
    }

    return ValidationResult.Success;
}

// Combining multiple validation results
var results = new[]
{
    ValidateUser(user),
    ValidateAddress(address),
    ValidatePayment(payment)
};

var combinedResult = ValidationResult.Combine(results);
```

#### `UploadResult` - For File Upload Operations

```csharp
public async Task<UploadResult> UploadFileAsync(Stream fileStream)
{
    try
    {
        // Upload logic
        return new UploadResult(UploadStatus.Success, "File uploaded successfully");
    }
    catch (Exception ex)
    {
        return new UploadResult(UploadStatus.Failure, ex.Message);
    }
}
```

### Legacy Result Types

These types provide backwards compatibility while leveraging the new implementation:

```csharp
// Old style - string-based errors
public Result ProcessLegacy()
{
    try
    {
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure("Operation failed", ex);
    }
}

// Old style with value
public Result<int> ProcessLegacyWithValue()
{
    return Result<int>.Success(42);
}
```

## Best Practices

### 1. Choose the Right Result Type

- Use `Result<TError>` when you only need error information
- Use `Result<T, TError>` when you need to return a value
- Use `ValidationResult` for validation scenarios
- Use legacy types only for backwards compatibility

### 2. Error Handling

```csharp
// Define specific error types for different scenarios
public record NotFoundError : ResultError
{
    public NotFoundError(string message) : base(message) { }
    public string ResourceType { get; init; }
}

public record ValidationError : ResultError
{
    public ValidationError(string message) : base(message) { }
    public string PropertyName { get; init; }
    public object AttemptedValue { get; init; }
}

// Use pattern matching for handling different states
result.Match(
    onSuccess: () => Console.WriteLine("Success"),
    onFailure: (error, ex) => LogError(error, ex),
    onPartialSuccess: error => HandlePartial(error)
);
```

### 3. Async Operations

```csharp
public async ValueTask<Result<Data, ApiError>> FetchDataAsync(
    string id,
    CancellationToken cancellationToken = default)
{
    try
    {
        var data = await _api.GetDataAsync(id, cancellationToken);
        return Result<Data, ApiError>.Success(data);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return Result<Data, ApiError>.Failure(
            new ApiError("Failed to fetch data"), 
            ex);
    }
}
```

### 4. Composition and Chaining

```csharp
public async Task<Result<OrderConfirmation, OrderError>> ProcessOrderAsync(Order order)
{
    return await ValidateOrder(order)  // Returns Result<Order, OrderError>
        .MapAsync(async validated => 
            await CalculateTotals(validated))  // Transform to new value
        .BindAsync(async totals => 
            await ProcessPayment(totals))  // Chain another result
        .MapAsync(async payment => 
            await CreateConfirmation(payment));  // Final transformation
}
```

### 5. Performance Considerations

- Use object pooling for high-frequency operations
- Leverage lazy evaluation when appropriate
- Consider using ValueTask for short-running async operations

```csharp
// Object pooling is handled automatically by the Result types
public Result<int, ProcessError> ProcessValue(int value)
{
    // The Result will be retrieved from the pool
    return Result<int, ProcessError>.Success(value);
}  // The Result will be returned to the pool when disposed

// Lazy evaluation
public Result<ExpensiveData, DataError> GetData()
{
    return Result<ExpensiveData, DataError>.LazySuccess(
        () => ComputeExpensiveData());
}
```

## Advanced Scenarios

### Custom Result Types

```csharp
public class ApiResult<T> : Result<T, ApiError>
{
    private ApiResult(T value, ResultState state, ApiError? error = null)
        : base(value, state, error)
    {
        StatusCode = error?.StatusCode ?? 200;
    }

    public int StatusCode { get; }

    public static ApiResult<T> Ok(T value)
        => new(value, ResultState.Success);

    public static ApiResult<T> NotFound(string message)
        => new(default!, ResultState.Failure, 
            new ApiError(message) { StatusCode = 404 });
}
```

### Integration with Dependency Injection

```csharp
public interface IResultFactory
{
    Result<T, TError> Create<T, TError>(T value)
        where TError : ResultError;
    
    ValidationResult CreateValidation();
}

public class ResultFactory : IResultFactory
{
    public Result<T, TError> Create<T, TError>(T value)
        where TError : ResultError
        => Result<T, TError>.Success(value);
    
    public ValidationResult CreateValidation()
        => ValidationResult.Success;
}

// In your startup:
services.AddSingleton<IResultFactory, ResultFactory>();
```

### Testing

```csharp
[Fact]
public void Success_Result_Should_Have_Value()
{
    // Arrange
    var value = 42;
    
    // Act
    var result = Result<int, TestError>.Success(value);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(value, result.Value);
    Assert.Null(result.Error);
}

[Fact]
public void Validation_Should_Combine_Results()
{
    // Arrange
    var results = new[]
    {
        ValidationResult.Failed("Error 1"),
        ValidationResult.Success,
        ValidationResult.Failed("Error 2")
    };
    
    // Act
    var combined = ValidationResult.Combine(results);
    
    // Assert
    Assert.False(combined.IsValid);
    Assert.Contains("Error 1", combined.ErrorMessage);
    Assert.Contains("Error 2", combined.ErrorMessage);
}
```
