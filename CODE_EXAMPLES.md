# Code Examples: Result Pattern in DropBear.Codex

**Version:** 2025.10.0+
**Last Updated:** October 2025

This document provides practical, real-world code examples for using the Result pattern across DropBear.Codex projects.

---

## Table of Contents

1. [Core Result Patterns](#core-result-patterns)
2. [Notifications Examples](#notifications-examples)
3. [StateManagement Examples](#statemanagement-examples)
4. [Tasks Examples](#tasks-examples)
5. [Blazor Examples](#blazor-examples)
6. [Workflow Examples](#workflow-examples)
7. [Error Handling Patterns](#error-handling-patterns)
8. [Testing with Results](#testing-with-results)

---

## Core Result Patterns

### Basic Result Creation

```csharp
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

// Success with value
var success = Result<int, SimpleError>.Success(42);

// Failure with error
var failure = Result<int, SimpleError>.Failure(
    new SimpleError("Operation failed")
);

// Success without value (using Unit)
var voidSuccess = Result<Unit, SimpleError>.Success(Unit.Value);
```

### Using Match for Control Flow

```csharp
var result = DivideNumbers(10, 2);

result.Match(
    onSuccess: value => Console.WriteLine($"Result: {value}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);

// Or with return value
var message = result.Match(
    onSuccess: value => $"Success: {value}",
    onFailure: error => $"Failed: {error.Message}"
);
```

### Chaining Operations with Map

```csharp
// Transform successful values
var result = GetUser(userId)
    .Map(user => user.Email)
    .Map(email => email.ToLowerInvariant());

// result is Result<string, UserError>
```

### Chaining Operations with Bind

```csharp
// Chain operations that return Results
var result = GetUser(userId)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user));

// Each step can fail independently
```

### Async Operations

```csharp
// Async Map
var result = await GetUserAsync(userId)
    .MapAsync(async user => await EnrichUserDataAsync(user));

// Async Bind
var result = await GetUserAsync(userId)
    .BindAsync(async user => await SendWelcomeEmailAsync(user));

// Full async pipeline
var result = await GetUserAsync(userId)
    .MapAsync(async user => await EnrichUserDataAsync(user))
    .BindAsync(async user => await ValidateUserAsync(user))
    .BindAsync(async user => await SaveUserAsync(user));
```

### Tap for Side Effects

```csharp
// Execute side effects without changing the Result
var result = GetUser(userId)
    .Tap(user => _logger.LogInformation("Found user: {UserId}", user.Id))
    .Map(user => user.ToDto())
    .Tap(dto => _metrics.IncrementCounter("users_converted"));
```

---

## Notifications Examples

### Example 1: Send Notification with Error Handling

```csharp
using DropBear.Codex.Notifications;
using DropBear.Codex.Notifications.Errors;

public class NotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationService> _logger;

    public async Task<Result<Unit, NotificationError>> SendUserNotificationAsync(
        int userId,
        string message,
        CancellationToken cancellationToken = default)
    {
        // Create notification
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };

        // Add to repository
        var addResult = await _repository.AddAsync(notification, cancellationToken);
        if (!addResult.IsSuccess)
        {
            _logger.LogError("Failed to add notification: {Error}", addResult.Error.Message);
            return Result<Unit, NotificationError>.Failure(addResult.Error);
        }

        // Send via channel
        var sendResult = await SendViaChannel(notification);
        if (!sendResult.IsSuccess)
        {
            // Rollback: Remove from repository
            await _repository.DeleteAsync(notification.Id, cancellationToken);
            return sendResult;
        }

        return Result<Unit, NotificationError>.Success(Unit.Value);
    }
}
```

### Example 2: Get Notifications with Fallback

```csharp
public async Task<IReadOnlyList<Notification>> GetUserNotificationsWithFallbackAsync(
    int userId,
    CancellationToken cancellationToken = default)
{
    var result = await _repository.GetByUserIdAsync(userId, cancellationToken);

    return result.Match(
        onSuccess: notifications => notifications,
        onFailure: error =>
        {
            _logger.LogWarning("Failed to get notifications for user {UserId}: {Error}",
                userId, error.Message);
            return Array.Empty<Notification>();
        }
    );
}
```

### Example 3: Functional Pipeline

```csharp
public async Task<Result<NotificationDto, NotificationError>> ProcessNotificationAsync(
    int notificationId,
    CancellationToken cancellationToken = default)
{
    return await _repository.GetByIdAsync(notificationId, cancellationToken)
        .MapAsync(async notification =>
        {
            notification.MarkAsRead();
            return notification;
        })
        .BindAsync(async notification =>
            await _repository.UpdateAsync(notification, cancellationToken))
        .MapAsync(async _ =>
            await GetNotificationDtoAsync(notificationId, cancellationToken));
}
```

---

## StateManagement Examples

### Example 1: Safe Snapshot Manager Construction

```csharp
using DropBear.Codex.StateManagement;
using DropBear.Codex.StateManagement.Errors;

public class GameStateService
{
    public Result<ISimpleSnapshotManager<GameState>, BuilderError> CreateSnapshotManager()
    {
        return new SnapshotBuilder<GameState>()
            .TryWithSnapshotInterval(TimeSpan.FromMinutes(5))
            .Bind(b => b.TryWithRetentionTime(TimeSpan.FromHours(24)))
            .Bind(b => b.TryWithMaxSnapshots(100))
            .Bind(b => b.TryBuild());
    }
}
```

### Example 2: Snapshot Save/Restore with Error Handling

```csharp
public class SaveGameManager
{
    private readonly ISimpleSnapshotManager<GameState> _snapshotManager;

    public async Task<Result<int, SnapshotError>> SaveGameAsync(GameState state)
    {
        var saveResult = _snapshotManager.SaveState(state);

        return saveResult.Match(
            onSuccess: version =>
            {
                _logger.LogInformation("Game saved as version {Version}", version);
                return Result<int, SnapshotError>.Success(version);
            },
            onFailure: error =>
            {
                _logger.LogError("Failed to save game: {Error}", error.Message);
                return Result<int, SnapshotError>.Failure(error);
            }
        );
    }

    public async Task<Result<GameState, SnapshotError>> LoadGameAsync(int version)
    {
        var restoreResult = _snapshotManager.RestoreState(version);

        if (!restoreResult.IsSuccess)
        {
            _logger.LogWarning("Failed to load version {Version}, loading latest", version);

            // Fallback to latest snapshot
            var latestResult = _snapshotManager.GetLatestSnapshot();
            if (latestResult.IsSuccess)
            {
                return Result<GameState, SnapshotError>.Success(latestResult.Value);
            }

            return Result<GameState, SnapshotError>.Failure(
                SnapshotError.NotFound(version)
            );
        }

        return restoreResult;
    }
}
```

---

## Tasks Examples

### Example 1: Implementing ITask with Result Pattern

```csharp
using DropBear.Codex.Tasks;
using DropBear.Codex.Tasks.Errors;

public class EmailSenderTask : ITask
{
    private readonly IEmailService _emailService;
    private string _recipient;
    private string _subject;

    public string Name => "SendEmailTask";
    public string Description => "Sends an email to specified recipient";

    public Result<Unit, TaskValidationError> Validate()
    {
        if (string.IsNullOrWhiteSpace(_recipient))
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty("Recipient", "Recipient is required")
            );
        }

        if (!IsValidEmail(_recipient))
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty("Recipient", "Invalid email format")
            );
        }

        if (string.IsNullOrWhiteSpace(_subject))
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty("Subject", "Subject is required")
            );
        }

        return Result<Unit, TaskValidationError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendEmailAsync(_recipient, _subject, cancellationToken);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, TaskExecutionError>.Cancelled(
                TaskExecutionError.Cancelled(Name)
            );
        }
        catch (TimeoutException)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Timeout(Name, TimeSpan.FromSeconds(30))
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

### Example 2: Task Execution with Cache

```csharp
public class TaskRunner
{
    private readonly SharedCache _cache;

    public async Task<Result<Unit, TaskExecutionError>> RunTaskWithCacheAsync(
        ITask task,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var cacheKey = $"task:{task.Name}";
        var cachedResult = _cache.Get<DateTime>(cacheKey);

        if (cachedResult.IsSuccess)
        {
            var lastRun = cachedResult.Value;
            if (DateTime.UtcNow - lastRun < TimeSpan.FromMinutes(5))
            {
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }
        }

        // Validate task
        var validationResult = task.Validate();
        if (!validationResult.IsSuccess)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Failed(task.Name, "Validation failed")
            );
        }

        // Execute task
        var executionResult = await task.ExecuteAsync(cancellationToken);

        if (executionResult.IsSuccess)
        {
            // Update cache
            _cache.Set(cacheKey, DateTime.UtcNow);
        }

        return executionResult;
    }
}
```

---

## Blazor Examples

### Example 1: Form Validation

```csharp
@page "/register"
@inject ValidationHelper ValidationHelper

<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <DropBearValidationErrorsComponent ValidationResult="@_validationResult" />

    <InputText @bind-Value="_model.Email" />
    <InputText @bind-Value="_model.Password" type="password" />

    <button type="submit">Register</button>
</EditForm>

@code {
    private RegisterModel _model = new();
    private ValidationResult? _validationResult;

    private async Task HandleValidSubmit()
    {
        // Validate using Core's ValidationResult
        _validationResult = ValidationHelper.ValidateModel(_model);

        if (_validationResult.IsValid)
        {
            await RegisterUserAsync(_model);
        }
    }

    private async Task RegisterUserAsync(RegisterModel model)
    {
        var result = await _userService.RegisterAsync(model);

        result.Match(
            onSuccess: user => NavigationManager.NavigateTo("/success"),
            onFailure: error =>
            {
                // Convert service error to validation error
                _validationResult = ValidationResult.Failed(
                    ValidationError.ForProperty("General", error.Message)
                );
                StateHasChanged();
            }
        );
    }
}
```

### Example 2: File Upload with Result

```csharp
@page "/upload"

<InputFile OnChange="@HandleFileSelected" />

@if (_uploadResult != null)
{
    @if (_uploadResult.IsSuccess)
    {
        <p class="success">Upload successful!</p>
    }
    else
    {
        <p class="error">@_uploadResult.Message</p>
    }
}

@code {
    private UploadResult? _uploadResult;

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;

        if (file.Size > 10_000_000) // 10MB limit
        {
            _uploadResult = UploadResult.Failure("File too large (max 10MB)");
            return;
        }

        _uploadResult = UploadResult.Uploading();

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10_000_000);
            var result = await _fileService.UploadAsync(stream, file.Name);

            _uploadResult = result.IsSuccess
                ? UploadResult.Success()
                : UploadResult.Failure(result.Error);
        }
        catch (Exception ex)
        {
            _uploadResult = UploadResult.Failure($"Upload failed: {ex.Message}");
        }
    }
}
```

---

## Workflow Examples

### Example 1: Order Processing Workflow with Result Conversion

```csharp
using DropBear.Codex.Workflow;
using DropBear.Codex.Workflow.Results;

public class OrderProcessingService
{
    private readonly IWorkflowEngine _workflowEngine;

    public async Task<Result<Order, OperationError>> ProcessOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var definition = BuildOrderWorkflow();
        var context = new OrderContext { Order = order };

        var workflowResult = await _workflowEngine.ExecuteAsync(
            definition,
            context,
            cancellationToken
        );

        // Convert WorkflowResult to Core Result for API response
        return workflowResult.ToResult()
            .Map(ctx => ctx.Order);
    }

    public async Task<Result<Order, OrderError>> ProcessOrderWithCustomErrorAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var definition = BuildOrderWorkflow();
        var context = new OrderContext { Order = order };

        var workflowResult = await _workflowEngine.ExecuteAsync(
            definition,
            context,
            cancellationToken
        );

        // Convert to custom error type
        return workflowResult.ToResult(wfResult => new OrderError(
            orderNumber: order.OrderNumber,
            stage: wfResult.IsSuspended ? "Suspended" : "Failed",
            reason: wfResult.ErrorMessage ?? "Unknown error",
            correlationId: wfResult.CorrelationId
        )).Map(ctx => ctx.Order);
    }
}
```

### Example 2: Workflow with Metrics

```csharp
public class MetricsTrackingService
{
    public async Task<Result<ProcessingReport, OperationError>> ProcessWithMetricsAsync(
        WorkflowDefinition<DataContext> definition,
        DataContext context,
        CancellationToken cancellationToken)
    {
        var workflowResult = await _workflowEngine.ExecuteAsync(
            definition,
            context,
            cancellationToken
        );

        if (!workflowResult.IsSuccess)
        {
            return Result<ProcessingReport, OperationError>.Failure(
                OperationError.ForOperation(
                    "WorkflowExecution",
                    workflowResult.ErrorMessage ?? "Workflow failed"
                )
            );
        }

        var report = new ProcessingReport
        {
            Success = true,
            Duration = workflowResult.Metrics?.TotalDuration ?? TimeSpan.Zero,
            StepsExecuted = workflowResult.Metrics?.StepsExecuted ?? 0,
            CorrelationId = workflowResult.CorrelationId
        };

        return Result<ProcessingReport, OperationError>.Success(report);
    }
}
```

---

## Error Handling Patterns

### Pattern 1: Convert Between Error Types

```csharp
public Result<User, DomainError> GetUserForDomain(int userId)
{
    var result = _repository.GetUser(userId);

    if (!result.IsSuccess)
    {
        // Map repository error to domain error
        var domainError = result.Error.Code switch
        {
            "REPO_NOT_FOUND" => DomainError.UserNotFound(userId),
            "REPO_UNAUTHORIZED" => DomainError.AccessDenied(userId),
            _ => DomainError.RepositoryError(result.Error.Message)
        };

        return Result<User, DomainError>.Failure(domainError);
    }

    return Result<User, DomainError>.Success(result.Value);
}
```

### Pattern 2: Aggregate Multiple Errors

```csharp
public Result<Report, ValidationError> GenerateReport(ReportRequest request)
{
    var errors = new List<ValidationError>();

    // Validate all fields
    if (string.IsNullOrEmpty(request.Title))
        errors.Add(ValidationError.ForProperty("Title", "Title is required"));

    if (request.StartDate > request.EndDate)
        errors.Add(ValidationError.ForProperty("DateRange", "Start date must be before end date"));

    if (request.MaxResults <= 0)
        errors.Add(ValidationError.ForProperty("MaxResults", "Must be positive"));

    if (errors.Any())
    {
        var aggregated = ValidationResult.Failed(errors);
        return Result<Report, ValidationError>.Failure(
            ValidationError.ForProperty("Report", $"{errors.Count} validation errors")
        );
    }

    var report = CreateReport(request);
    return Result<Report, ValidationError>.Success(report);
}
```

### Pattern 3: Retry Logic

```csharp
public async Task<Result<T, OperationError>> ExecuteWithRetryAsync<T>(
    Func<Task<Result<T, OperationError>>> operation,
    int maxRetries = 3,
    TimeSpan delay = default)
{
    delay = delay == default ? TimeSpan.FromSeconds(1) : delay;
    Result<T, OperationError> result = null!;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        result = await operation();

        if (result.IsSuccess)
            return result;

        if (attempt < maxRetries - 1)
        {
            await Task.Delay(delay * (attempt + 1));
        }
    }

    return result;
}

// Usage
var result = await ExecuteWithRetryAsync(
    async () => await _service.GetDataAsync(id),
    maxRetries: 3
);
```

### Pattern 4: Fallback Chain

```csharp
public async Task<Result<Config, ConfigError>> LoadConfigAsync()
{
    // Try primary source
    var primaryResult = await LoadFromPrimarySourceAsync();
    if (primaryResult.IsSuccess)
        return primaryResult;

    _logger.LogWarning("Primary config source failed, trying backup");

    // Try backup source
    var backupResult = await LoadFromBackupSourceAsync();
    if (backupResult.IsSuccess)
        return backupResult;

    _logger.LogWarning("Backup config source failed, using defaults");

    // Fall back to defaults
    return Result<Config, ConfigError>.Success(Config.Default);
}
```

---

## Testing with Results

### Example 1: Testing Success Cases

```csharp
[Fact]
public async Task GetUser_WithValidId_ReturnsSuccess()
{
    // Arrange
    var userId = 123;
    var repository = new UserRepository();

    // Act
    var result = await repository.GetUserAsync(userId);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal(userId, result.Value.Id);
}
```

### Example 2: Testing Failure Cases

```csharp
[Fact]
public async Task GetUser_WithInvalidId_ReturnsNotFoundError()
{
    // Arrange
    var userId = -1;
    var repository = new UserRepository();

    // Act
    var result = await repository.GetUserAsync(userId);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.NotNull(result.Error);
    Assert.Equal("USER_NOT_FOUND", result.Error.Code);
    Assert.Contains("not found", result.Error.Message, StringComparison.OrdinalIgnoreCase);
}
```

### Example 3: Testing Match Behavior

```csharp
[Fact]
public async Task ProcessUser_Success_ExecutesSuccessPath()
{
    // Arrange
    var successCalled = false;
    var failureCalled = false;

    var result = Result<User, UserError>.Success(new User { Id = 1 });

    // Act
    result.Match(
        onSuccess: _ => successCalled = true,
        onFailure: _ => failureCalled = true
    );

    // Assert
    Assert.True(successCalled);
    Assert.False(failureCalled);
}
```

### Example 4: Testing Chaining

```csharp
[Fact]
public async Task UserPipeline_WithValidData_ProcessesSuccessfully()
{
    // Arrange
    var userId = 1;

    // Act
    var result = await GetUserAsync(userId)
        .MapAsync(async user => await EnrichUserAsync(user))
        .BindAsync(async user => await ValidateUserAsync(user))
        .MapAsync(async user => user.ToDto());

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.IsType<UserDto>(result.Value);
}
```

---

## Additional Resources

- **Migration Guide:** See `MIGRATION_GUIDE.md` for upgrading existing code
- **Quick Reference:** See `QUICK_REFERENCE.md` for pattern summaries
- **Architecture:** See `CLAUDE.md` for detailed architecture documentation

---

**Last Updated:** October 2025
**Status:** Complete - All 9 projects using Result pattern
