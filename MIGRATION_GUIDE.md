# Migration Guide: Result Pattern Adoption

**Version:** 2025.10.0+
**Date:** October 2025
**Status:** ✅ Complete - All 9 projects migrated

---

## Table of Contents

1. [Overview](#overview)
2. [What Changed](#what-changed)
3. [Why This Change](#why-this-change)
4. [Breaking Changes](#breaking-changes)
5. [Migration by Project](#migration-by-project)
6. [Common Patterns](#common-patterns)
7. [Troubleshooting](#troubleshooting)

---

## Overview

DropBear.Codex has been modernized to use **Railway-Oriented Programming (ROP)** with a comprehensive `Result<T, TError>` pattern. This eliminates exception-based error handling in favor of explicit, typed error returns.

**Alignment Status:** 95% (all 9 projects at 85-98%)

### Key Benefits

✅ **Type Safety** - Errors are strongly typed, not strings
✅ **Explicit Control Flow** - No hidden exceptions
✅ **Better IntelliSense** - Factory methods are discoverable
✅ **Functional Composition** - Chain operations with Map/Bind
✅ **Performance** - Reduced exception allocations

---

## What Changed

### Result Types (from Core)

```csharp
// Operation with no return value
Result<TError>

// Operation with return value
Result<T, TError>

// Example error types
ResultError (base)
├── SimpleError (message only)
├── CodedError (with error codes)
└── OperationError (multi-field)
```

### Before vs After

**Before (Exception-based):**
```csharp
try
{
    var user = repository.GetUser(id); // Throws if not found
    ProcessUser(user);
}
catch (NotFoundException ex)
{
    // Handle error
}
```

**After (Result-based):**
```csharp
var result = repository.GetUser(id);
result.Match(
    onSuccess: user => ProcessUser(user),
    onFailure: error => LogError(error)
);
```

---

## Why This Change

### Problems with Exception-Based Error Handling

1. **Hidden Control Flow** - Exceptions are invisible in method signatures
2. **Performance Cost** - Stack unwinding is expensive
3. **Compiler Can't Help** - No compile-time checking of error handling
4. **Inconsistent Usage** - Mix of exceptions and return codes
5. **Lost Context** - Exception messages lose structured information

### Benefits of Result Pattern

1. **Explicit in Signatures** - `Result<T, TError>` tells you errors are possible
2. **Compiler Enforced** - Must handle Success/Failure cases
3. **Composable** - Use Map/Bind/Match for functional pipelines
4. **Rich Error Context** - Typed errors with metadata, codes, severity
5. **Better Performance** - No stack unwinding overhead

---

## Breaking Changes

### Summary by Project

| Project | Breaking Changes | Migration Effort | Notes |
|---------|-----------------|------------------|-------|
| **Core** | ✅ None | None | New functionality only |
| **Notifications** | ⚠️ Major | High | All methods return Results |
| **StateManagement** | ⚠️ Moderate | Medium | New Try* methods added |
| **Hashing** | ✅ None | None | Already using Results |
| **Files** | ⚠️ Moderate | Medium | Builders have Try* variants |
| **Utilities** | ⚠️ Minor | Low | Removed legacy methods |
| **Tasks** | ⚠️ Major | High | ITask interface changed |
| **Blazor** | ⚠️ Moderate | Medium | ValidationResult replaced |
| **Serialization** | ⚠️ Minor | Low | Exceptions marked obsolete |
| **Workflow** | ✅ Minor | Low | New ToResult() methods |

### Deprecated/Obsolete

The following are marked `[Obsolete]`:
- `SerializationException` → Use `Result<T, SerializationError>`
- `DeserializationException` → Use `Result<T, DeserializationError>`
- `CompressionException` → Use `Result<T, CompressionError>`

The following have been removed:
- `ValidationResultExtensions` (Blazor) → Use Core's ValidationResult
- `NotificationCompatibilityExtensions` → Use Result-based APIs directly
- Legacy `DebounceAsync` overloads (Utilities) → Use new signatures

---

## Security-Related Breaking Changes (Version 2025.11.0)

### ⚠️ CRITICAL: Jumbler Password Obfuscation (Utilities Project)

**Security Grade Improvement:** B+ → A-

The Jumbler class has undergone major security improvements to address high-severity vulnerabilities:

#### What Changed

1. **Removed Default Encryption Key** (H1 - High Severity)
   - Default key `"QVANYCLEPW"` has been removed
   - `keyPhrase` parameter is now **required** (no longer optional)

2. **Increased PBKDF2 Iterations** (H2 - High Severity)
   - Changed from **10,000** → **600,000** iterations
   - Aligns with OWASP 2023 recommendations
   - 60x stronger resistance to brute-force attacks

3. **Random Salt Generation** (H3 - High Severity)
   - Each encryption operation now uses a unique 32-byte random salt
   - Prevents rainbow table and precomputation attacks
   - Salt is stored with encrypted data

4. **Format Version Update**
   - Version prefix changed: `.JuMbLe.02.` → `.JuMbLe.03.`
   - v03 format stores: `[32-byte salt][encrypted data]`
   - **NOT backward compatible** with v02 encrypted data

#### Migration Steps

**Step 1: Identify Jumbler Usage**

Search your codebase for:
```bash
git grep "Jumbler.JumblePassword\|Jumbler.UnJumblePassword"
```

**Step 2: Update Method Calls (Breaking Change)**

**Before (v02 - INSECURE):**
```csharp
// Used default key (security vulnerability)
var jumbled = Jumbler.JumblePassword(password);

// Optional custom key
var jumbled = Jumbler.JumblePassword(password, "MyKey123");
```

**After (v03 - SECURE):**
```csharp
// KeyPhrase is now REQUIRED - compilation error if omitted
var jumbled = Jumbler.JumblePassword(password, keyPhrase);

// Must always provide explicit key phrase
var result = Jumbler.JumblePassword(password, strongKeyPhrase);
if (result.IsSuccess)
{
    var encrypted = result.Value;
}
```

**Step 3: Generate Strong Key Phrases**

```csharp
// ❌ DON'T: Weak or predictable keys
var weakKey = "password123";
var weakKey2 = "MyAppName";

// ✅ DO: Strong, random, application-specific keys
using var rng = RandomNumberGenerator.Create();
var keyBytes = new byte[32];
rng.GetBytes(keyBytes);
var strongKey = Convert.ToBase64String(keyBytes);
// Result: "kX9mP2vQ8wR5tY6uI3oL7jH1nM4bV0cZ..."

// Or use a passphrase with high entropy
var strongKey = "MyApp-2025-Production-Encryption-Key-v1-a8f3c2d9";
```

**Step 4: Store Key Phrases Securely**

**❌ DON'T store in code:**
```csharp
// NEVER hardcode keys in source code
const string KEY = "my-secret-key"; // BAD!
```

**✅ DO use secure storage:**

**Option A: Azure Key Vault**
```csharp
var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
var secret = await client.GetSecretAsync("JumblerKeyPhrase");
var keyPhrase = secret.Value.Value;
```

**Option B: AWS Secrets Manager**
```csharp
var client = new AmazonSecretsManagerClient();
var request = new GetSecretValueRequest { SecretId = "JumblerKeyPhrase" };
var response = await client.GetSecretValueAsync(request);
var keyPhrase = response.SecretString;
```

**Option C: Environment Variables (less secure, ok for dev)**
```csharp
var keyPhrase = Environment.GetEnvironmentVariable("JUMBLER_KEY_PHRASE")
    ?? throw new InvalidOperationException("JUMBLER_KEY_PHRASE not configured");
```

**Option D: ASP.NET Core User Secrets (dev only)**
```bash
dotnet user-secrets set "JumblerKeyPhrase" "your-strong-key-here"
```
```csharp
var keyPhrase = configuration["JumblerKeyPhrase"];
```

**Step 5: Re-encrypt Existing Data**

If you have data encrypted with the old Jumbler (v02), you **must** decrypt and re-encrypt:

```csharp
// Migration helper method
public async Task<Result<Unit, JumblerError>> MigrateJumbledDataAsync(
    string oldJumbledValue,
    string newKeyPhrase)
{
    try
    {
        // 1. Decrypt with OLD Jumbler (keep old code temporarily)
        var decrypted = OldJumbler.UnJumblePassword(oldJumbledValue);
        if (!decrypted.IsSuccess)
        {
            return Result<Unit, JumblerError>.Failure(
                new JumblerError("Failed to decrypt old format"));
        }

        // 2. Re-encrypt with NEW Jumbler
        var reencrypted = Jumbler.JumblePassword(decrypted.Value, newKeyPhrase);
        if (!reencrypted.IsSuccess)
        {
            return Result<Unit, JumblerError>.Failure(
                new JumblerError("Failed to re-encrypt"));
        }

        // 3. Store new encrypted value
        await SaveToDatabase(reencrypted.Value);

        return Result<Unit, JumblerError>.Success(Unit.Value);
    }
    catch (Exception ex)
    {
        return Result<Unit, JumblerError>.Failure(
            new JumblerError($"Migration failed: {ex.Message}"), ex);
    }
}
```

**Step 6: Performance Considerations**

With 600,000 iterations, PBKDF2 key derivation is slower (intentionally):

```csharp
// Enable key caching (built-in, thread-safe)
// First call: ~200-500ms (depends on CPU)
var result1 = Jumbler.JumblePassword(password, keyPhrase); // Slow (derives key)

// Subsequent calls with same keyPhrase + salt: <1ms
var result2 = Jumbler.JumblePassword(password, keyPhrase); // Fast (cached)

// Clear cache when needed (e.g., on logout, key rotation)
Jumbler.ClearKeyCache();
```

#### Error Handling

**Null/Empty KeyPhrase:**
```csharp
var result = Jumbler.JumblePassword(password, null);
// result.IsSuccess = false
// result.Error.Message = "KeyPhrase must be provided. Default keys are not supported for security reasons."
```

**Invalid Format During Decryption:**
```csharp
var result = Jumbler.UnJumblePassword("invalid-format", keyPhrase);
// result.IsSuccess = false
// result.Error.Message = "Invalid jumbled password format: too short to contain salt."
```

#### Testing

**Unit Test Example:**
```csharp
[Fact]
public void Jumbler_RoundTrip_WithSecureKeyPhrase_Success()
{
    // Arrange
    var password = "MySecretPassword123!";
    var keyPhrase = GenerateSecureKeyPhrase(); // 32+ char random

    // Act: Encrypt
    var encrypted = Jumbler.JumblePassword(password, keyPhrase);
    Assert.True(encrypted.IsSuccess);

    // Act: Decrypt
    var decrypted = Jumbler.UnJumblePassword(encrypted.Value, keyPhrase);

    // Assert
    Assert.True(decrypted.IsSuccess);
    Assert.Equal(password, decrypted.Value);
}

[Fact]
public void Jumbler_RequiresKeyPhrase_ThrowsCompilationError()
{
    var password = "test";

    // This will NOT compile (keyPhrase is required parameter)
    // var result = Jumbler.JumblePassword(password);

    // Must provide keyPhrase
    var result = Jumbler.JumblePassword(password, "strong-key");
    Assert.True(result.IsSuccess);
}
```

#### Security Best Practices

1. **✅ DO:**
   - Use cryptographically random key phrases (32+ bytes)
   - Store keys in Azure Key Vault, AWS Secrets Manager, or similar
   - Use different keys for different environments (dev/staging/prod)
   - Rotate keys periodically (e.g., every 90 days)
   - Log failed decryption attempts for security monitoring

2. **❌ DON'T:**
   - Hardcode key phrases in source code
   - Commit keys to version control
   - Use weak/predictable keys like "password123"
   - Share keys between applications
   - Log decrypted values

#### Timeline & Support

- **Version 2025.10.x:** Old Jumbler (v02) with default keys
- **Version 2025.11.0+:** New Jumbler (v03) with required keys
- **Migration Period:** Keep old code for 90 days to decrypt legacy data
- **Deprecation:** Old format fully deprecated after 2026-02-01

---

## Migration by Project

### 1. Notifications Project

**Breaking Changes:**
- All repository methods return `Result<T, NotificationError>`
- All service methods return `Result<T, NotificationError>`

**Before:**
```csharp
var notification = await repository.GetByIdAsync(id); // Could throw
await repository.AddAsync(notification); // Could throw
```

**After:**
```csharp
var result = await repository.GetByIdAsync(id, cancellationToken);
if (result.IsSuccess)
{
    var notification = result.Value;

    var addResult = await repository.AddAsync(notification, cancellationToken);
    if (addResult.IsSuccess)
    {
        // Success
    }
}
```

**Using Match Pattern:**
```csharp
var result = await repository.GetByIdAsync(id, cancellationToken);
result.Match(
    onSuccess: notification => Console.WriteLine($"Found: {notification.Title}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

**Using Functional Composition:**
```csharp
var result = await repository.GetByIdAsync(id, cancellationToken)
    .BindAsync(async notification =>
    {
        notification.MarkAsRead();
        return await repository.UpdateAsync(notification, cancellationToken);
    });
```

**Error Handling:**
```csharp
// Create typed errors
var error = NotificationError.NotFound(notificationId);
var error2 = NotificationError.OperationFailed("Failed to send", "SendNotification");

// Return failures
return Result<Unit, NotificationError>.Failure(error);
```

---

### 2. StateManagement Project

**Breaking Changes:**
- `ISimpleSnapshotManager<T>` methods return `Result<T, SnapshotError>`
- `SnapshotBuilder<T>` has new `Try*` methods

**Before:**
```csharp
var builder = new SnapshotBuilder<MyContext>();
builder.WithSnapshotInterval(TimeSpan.FromMinutes(5)); // Could throw
builder.WithRetentionTime(TimeSpan.FromHours(24)); // Could throw
var manager = builder.Build(); // Could throw
```

**After (Safe Pattern):**
```csharp
var result = new SnapshotBuilder<MyContext>()
    .TryWithSnapshotInterval(TimeSpan.FromMinutes(5))
    .Bind(b => b.TryWithRetentionTime(TimeSpan.FromHours(24)))
    .Bind(b => b.TryBuild());

if (result.IsSuccess)
{
    var manager = result.Value;
}
```

**Snapshot Operations:**
```csharp
// Save state
var saveResult = manager.SaveState(context);
if (saveResult.IsSuccess)
{
    Console.WriteLine($"Saved as version {saveResult.Value}");
}

// Restore state
var restoreResult = manager.RestoreState(version);
restoreResult.Match(
    onSuccess: context => ProcessContext(context),
    onFailure: error => LogError(error)
);
```

**Error Types:**
```csharp
SnapshotError.NotFound(version);
SnapshotError.IntervalNotReached(TimeSpan.FromMinutes(1));
BuilderError.InvalidInterval("Interval must be positive");
```

---

### 3. Files Project

**Breaking Changes:**
- `BlobStorageFactory` returns `Result<IBlobStorage, StorageError>`
- `FileManagerBuilder` has new `Try*` methods
- `FileManager` query methods return `Result<T, FileOperationError>`

**Before:**
```csharp
var storage = BlobStorageFactory.CreateAzureBlobStorage(connectionString); // Could throw
var builder = new FileManagerBuilder();
builder.UseBlobStorage(storage); // Could throw
```

**After:**
```csharp
var storageResult = BlobStorageFactory.CreateAzureBlobStorage(connectionString);
if (storageResult.IsSuccess)
{
    var storage = storageResult.Value;

    var builderResult = new FileManagerBuilder().TryUseBlobStorage(storage);
    builderResult.Match(
        onSuccess: builder => { /* Use builder */ },
        onFailure: error => LogError(error)
    );
}
```

**Query Methods:**
```csharp
// Get container by type
var result = fileManager.GetContainerByContentType("application/pdf");
if (result.IsSuccess && result.Value != null)
{
    var container = result.Value;
    // Process container
}
```

**Error Types:**
```csharp
StorageError.InvalidInput("Connection string is required");
StorageError.CreationFailed("Failed to connect to blob storage");
FileOperationError.NotFound("Container", "hash123");
BuilderError.StorageNotConfigured();
```

---

### 4. Tasks Project

**Breaking Changes:**
- `ITask` interface changed - `Validate()` and `ExecuteAsync()` return Results
- `SharedCache` methods return `Result<T, CacheError>`
- `TaskDependencyResolver.TopologicalSort()` returns `Result<List<string>, TaskExecutionError>`

**Before:**
```csharp
public class MyTask : ITask
{
    public void Validate()
    {
        if (string.IsNullOrEmpty(Name))
            throw new InvalidOperationException("Name required");
    }

    public async Task ExecuteAsync(CancellationToken token)
    {
        // Execute logic
    }
}
```

**After:**
```csharp
public class MyTask : ITask
{
    public Result<Unit, TaskValidationError> Validate()
    {
        if (string.IsNullOrEmpty(Name))
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidName(Name)
            );

        return Result<Unit, TaskValidationError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(
        CancellationToken token)
    {
        try
        {
            // Execute logic
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, TaskExecutionError>.Cancelled(
                TaskExecutionError.Cancelled(Name)
            );
        }
        catch (Exception ex)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Failed(Name, ex.Message)
            );
        }
    }
}
```

**SharedCache Usage:**
```csharp
// Set value
var setResult = cache.Set("key", myValue);
if (!setResult.IsSuccess)
{
    Console.WriteLine($"Cache set failed: {setResult.Error.Message}");
}

// Get value
var getResult = cache.Get<MyType>("key");
getResult.Match(
    onSuccess: value => ProcessValue(value),
    onFailure: error => HandleCacheError(error)
);
```

---

### 5. Blazor Project

**Breaking Changes:**
- `ValidationHelper` returns Core's `ValidationResult` (not Blazor's)
- `UploadResult` is backed by `Result<Unit, FileUploadError>`
- Custom `ValidationResult`/`ValidationError` types removed

**Before:**
```csharp
var validationResult = ValidationHelper.ValidateModel(model);
if (validationResult.HasErrors)
{
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"{error.Parameter}: {error.ErrorMessage}");
    }
}
```

**After:**
```csharp
var validationResult = ValidationHelper.ValidateModel(model);
if (!validationResult.IsValid)
{
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.Message}");
    }
}
```

**UploadResult:**
```csharp
// Creating results (backward compatible)
var success = UploadResult.Success();
var failure = UploadResult.Failure("Upload failed");

// Access underlying Result
if (uploadResult.Result.IsSuccess)
{
    // Success
}

// Convert from Result
var result = Result<Unit, FileUploadError>.Success(Unit.Value);
var uploadResult = UploadResult.FromResult(result);
```

**Component Property Names:**
```razor
@* Before *@
@foreach (var error in Errors)
{
    <p>@error.Parameter: @error.ErrorMessage</p>
}

@* After *@
@foreach (var error in Errors)
{
    <p>@error.PropertyName: @error.Message</p>
}
```

---

### 6. Serialization Project

**Breaking Changes:**
- Exception types marked `[Obsolete]` (still work but show warnings)

**Migration:**
```csharp
// Before (throws exceptions)
try
{
    var bytes = serializer.Serialize(data);
}
catch (SerializationException ex)
{
    // Handle
}

// After (Result pattern - already implemented!)
var result = await serializer.SerializeAsync(data, cancellationToken);
result.Match(
    onSuccess: bytes => ProcessBytes(bytes),
    onFailure: error => LogError(error)
);
```

**Note:** All serializers already return Results. The exceptions are only used for backward compatibility and will be removed in a future version.

---

### 7. Workflow Project

**New Features (Non-Breaking):**
- `WorkflowResult<TContext>.ToResult()` - Convert to Core Result
- `WorkflowResult<TContext>.ToResult<TError>()` - Custom error mapping

**Usage:**
```csharp
var workflowResult = await engine.ExecuteAsync(definition, context, token);

// Convert to Core Result for pipeline integration
var coreResult = workflowResult.ToResult();

// Use in Result-based pipeline
var finalResult = coreResult
    .Map(ctx => TransformContext(ctx))
    .Bind(ctx => SaveToDatabase(ctx));

// Custom error mapping
var customResult = workflowResult.ToResult(wfResult =>
    new MyCustomError($"Workflow {wfResult.WorkflowId} failed")
);
```

---

## Common Patterns

### Pattern 1: Simple Success/Failure Check

```csharp
var result = SomeOperation();
if (result.IsSuccess)
{
    var value = result.Value;
    // Use value
}
else
{
    var error = result.Error;
    // Handle error
}
```

### Pattern 2: Match Expression

```csharp
var result = SomeOperation();
result.Match(
    onSuccess: value => ProcessValue(value),
    onFailure: error => LogError(error)
);
```

### Pattern 3: Functional Chaining

```csharp
var result = GetUser(id)
    .Map(user => user.ToDto())
    .Bind(dto => ValidateDto(dto))
    .Bind(dto => SaveDto(dto));
```

### Pattern 4: Async Chaining

```csharp
var result = await GetUserAsync(id)
    .MapAsync(async user => await EnrichUserAsync(user))
    .BindAsync(async user => await SaveUserAsync(user));
```

### Pattern 5: Early Return

```csharp
public Result<User, UserError> GetUserProfile(int id)
{
    var userResult = _repository.GetUser(id);
    if (!userResult.IsSuccess)
        return Result<User, UserError>.Failure(/* map error */);

    var user = userResult.Value;

    var permissionResult = CheckPermissions(user);
    if (!permissionResult.IsSuccess)
        return Result<User, UserError>.Failure(/* map error */);

    return Result<User, UserError>.Success(user);
}
```

### Pattern 6: Combine Multiple Results

```csharp
var result1 = Operation1();
var result2 = Operation2();

if (result1.IsSuccess && result2.IsSuccess)
{
    // Both succeeded
    Process(result1.Value, result2.Value);
}
else
{
    // At least one failed
    var errors = new List<string>();
    if (!result1.IsSuccess) errors.Add(result1.Error.Message);
    if (!result2.IsSuccess) errors.Add(result2.Error.Message);
}
```

### Pattern 7: Factory Methods for Errors

```csharp
// Use factory methods for consistency
return Result<User, UserError>.Failure(
    UserError.NotFound(userId)
);

return Result<Unit, ValidationError>.Failure(
    ValidationError.ForProperty("Email", "Invalid format")
);

return Result<Config, ConfigError>.Failure(
    ConfigError.InvalidValue("Timeout", "Must be positive")
);
```

---

## Troubleshooting

### "Result type is too verbose"

**Problem:**
```csharp
Result<User, UserError> result = GetUser(id);
```

**Solution:** Use `var` and let type inference work:
```csharp
var result = GetUser(id); // Compiler knows the type
```

### "How do I convert between error types?"

**Use Map or create new Result:**
```csharp
// Map to different error type
var notificationResult = repository.GetNotification(id);
var customResult = notificationResult.IsSuccess
    ? Result<Notification, MyError>.Success(notificationResult.Value)
    : Result<Notification, MyError>.Failure(
        new MyError(notificationResult.Error.Message)
    );
```

### "I need to throw an exception"

**For truly exceptional cases, you can:**
```csharp
var result = SomeOperation();
var value = result.ValueOrThrow(); // Throws if failure

// Or custom exception
if (!result.IsSuccess)
    throw new InvalidOperationException(result.Error.Message);
```

### "How do I use with existing try/catch code?"

**Gradual migration:**
```csharp
try
{
    var result = NewResultBasedMethod();
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException(result.Error.Message);
    }

    var value = result.Value;
    // Use value
}
catch (Exception ex)
{
    // Existing error handling
}
```

### "Result chaining is confusing"

**Start simple, then refactor:**
```csharp
// Step 1: Explicit checks
var userResult = GetUser(id);
if (!userResult.IsSuccess) return /* error */;

var user = userResult.Value;

var enrichResult = EnrichUser(user);
if (!enrichResult.IsSuccess) return /* error */;

var enrichedUser = enrichResult.Value;

// Step 2: Refactor to chaining when comfortable
var result = GetUser(id)
    .Bind(user => EnrichUser(user));
```

---

## Best Practices

### ✅ DO

- **Use factory methods** for creating errors (NotFound, InvalidInput, etc.)
- **Use Match()** for handling both success and failure cases
- **Use Map/Bind** for functional composition when appropriate
- **Preserve exceptions** when creating Results from catch blocks
- **Add metadata** to errors for better debugging
- **Use Unit.Value** for Result types with no meaningful return value

### ❌ DON'T

- **Don't throw exceptions** for expected errors
- **Don't ignore IsSuccess** without checking - handle both cases
- **Don't create Results with null values** - use proper error instead
- **Don't lose error context** when converting between error types
- **Don't use ValueOrThrow()** unless truly needed

---

## Additional Resources

- **Core Documentation:** See `DropBear.Codex.Core` XML docs
- **Code Examples:** See `QUICK_REFERENCE.md`
- **Implementation Status:** See `IMPLEMENTATION_STATUS.md`
- **Architecture Guide:** See `CLAUDE.md`

---

## Support

If you encounter issues during migration:
1. Check this guide for your specific project
2. Review the code examples in `QUICK_REFERENCE.md`
3. Examine the unit tests for usage patterns
4. Open an issue at https://github.com/tkuchel/DropBear.Codex/issues

---

**Last Updated:** October 2025
**Migration Status:** ✅ Complete - All projects at 85-98% alignment
