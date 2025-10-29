# DropBear.Codex Quick Reference Guide

**For Developers Working on Modernization**

---

## Quick Patterns

### Converting Exception-Throwing Methods

```csharp
// ❌ OLD WAY
public async Task<T> MethodAsync()
{
    if (invalid) throw new ArgumentException();
    return await Operation();
}

// ✅ NEW WAY
public async ValueTask<Result<T, TError>> MethodAsync(
    CancellationToken cancellationToken = default)
{
    if (invalid)
        return Result<T, TError>.Failure(TError.ValidationFailed());

    try
    {
        var result = await Operation(cancellationToken).ConfigureAwait(false);
        return Result<T, TError>.Success(result);
    }
    catch (Exception ex)
    {
        return Result<T, TError>.Failure(TError.FromException(ex), ex);
    }
}
```

### Creating Error Types

```csharp
public sealed record ProjectError : ResultError
{
    public ProjectError(string message) : base(message) { }

    public static ProjectError NotFound(Guid id) =>
        new($"Item {id} not found")
        {
            Code = "NOT_FOUND",
            Category = ErrorCategory.Business
        };
}
```

### Using Validation

```csharp
var builder = ValidationBuilder<T>.For(value);
builder
    .Property(x => x.Name, nameof(value.Name))
        .NotNullOrWhiteSpace()
        .MaxLength(100)
    .Property(x => x.Age, nameof(value.Age))
        .GreaterThan(0);

var validation = builder.Build();
if (!validation.IsValid)
    return Result<T, ValidationError>.Failure(validation.Errors.First());
```

---

## Performance Quick Wins

### 1. Use Span for Strings
```csharp
// Instead of: string.Substring()
ReadOnlySpan<char> part = input.AsSpan(0, 10);
```

### 2. Use ArrayPool
```csharp
var buffer = ArrayPool<byte>.Shared.Rent(size);
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

### 3. Use ValueTask
```csharp
public ValueTask<Result<T, TError>> Method() // Instead of Task
```

### 4. Use FrozenDictionary
```csharp
private static readonly FrozenDictionary<string, int> Lookup =
    new Dictionary<string, int> { ... }.ToFrozenDictionary();
```

---

## Security Checklist

- [ ] Use `CryptographicOperations.FixedTimeEquals()` for hash/password comparison
- [ ] Use `CryptographicOperations.ZeroMemory()` to clear sensitive data
- [ ] Use `RandomNumberGenerator` for cryptographic random values
- [ ] Validate all inputs before processing
- [ ] Use `ReadOnlySpan<char>` for passwords (avoid string copies)

---

## Common Mistakes to Avoid

### ❌ DON'T
```csharp
// Don't use string for errors
return Result.Failure("Error message");

// Don't throw for expected errors
throw new InvalidOperationException("Failed");

// Don't forget ConfigureAwait(false)
await SomeMethodAsync();

// Don't allocate in loops
for (int i = 0; i < 1000; i++)
{
    var list = new List<int>(); // Allocates 1000 times!
}
```

### ✅ DO
```csharp
// Use typed errors
return Result<T, TError>.Failure(TError.OperationFailed());

// Return Results
return Result<T, TError>.Failure(error, ex);

// Always use ConfigureAwait
await SomeMethodAsync().ConfigureAwait(false);

// Allocate once outside loop
var list = new List<int>(1000);
for (int i = 0; i < 1000; i++)
{
    list.Add(i);
}
```

---

## File Locations

- **Error Types:** `[Project]/Errors/`
- **Interfaces:** `[Project]/Interfaces/`
- **Models:** `[Project]/Models/`
- **Services:** `[Project]/Services/`

---

## Testing Templates

### Unit Test
```csharp
[Fact]
public async Task Method_ValidInput_ReturnsSuccess()
{
    var result = await _sut.MethodAsync(validInput);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
}

[Fact]
public async Task Method_InvalidInput_ReturnsFailure()
{
    var result = await _sut.MethodAsync(invalidInput);

    result.IsSuccess.Should().BeFalse();
    result.Error.Should().BeOfType<ExpectedError>();
}
```

### Benchmark
```csharp
[MemoryDiagnoser]
public class MyBenchmarks
{
    [Benchmark(Baseline = true)]
    public void OldWay() { }

    [Benchmark]
    public void NewWay() { }
}
```

---

## Useful Commands

```bash
# Build all projects
dotnet build

# Run specific project tests
dotnet test DropBear.Codex.Core.Tests

# Run benchmarks
dotnet run -c Release --project Benchmarks

# Find TODO comments
grep -r "TODO" --include="*.cs"

# Find throwing methods
grep -r "throw new" --include="*.cs"

# Check for string errors (should use error types)
grep -r 'Result.*Failure("' --include="*.cs"
```

---

## Priority Order

1. **Critical:** Notifications, StateManagement, Hashing
2. **High:** Files, Tasks
3. **Medium:** Blazor, Workflow
4. **Low:** Serialization, Utilities

---

**Need Help?** See `MODERNIZATION_PLAN.md` for detailed guidance.
