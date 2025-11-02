# LoggerMessage Source Generation Migration Strategy

## Overview

This document outlines the strategy for migrating DropBear.Codex from direct Serilog usage to Microsoft.Extensions.Logging (MEL) with Serilog as a sink, enabling LoggerMessage source generation for 20-40% logging performance improvement.

## Current State

**Logging Architecture:**
- 506 traditional log statements across 49 files
- Uses Serilog directly via `LoggerFactory.Logger.ForContext<T>()`
- WorkflowEngine already uses MEL (`Microsoft.Extensions.Logging.ILogger<T>`) with LoggerMessage source generation

**Distribution:**
- Serialization: 223 occurrences (24 files)
- Files: 128 occurrences (8 files)
- Tasks: 88 occurrences (11 files)
- Blazor: 67 occurrences (5 files)
- Workflow: Already migrated ✅

## Target Architecture

### Logging Flow
```
Application Code
  ↓
Microsoft.Extensions.Logging.ILogger<T>
  ↓
Serilog Sink (via UseSerilog())
  ↓
Configured Outputs (Console, File, etc.)
```

### Benefits
1. **Performance**: LoggerMessage source generation (20-40% improvement)
2. **Compatibility**: MEL is the standard .NET logging interface
3. **Features**: Retain all Serilog features (structured logging, sinks, enrichers)
4. **Future-proof**: Compatible with .NET's logging ecosystem

## Migration Strategy

### Phase 1: Infrastructure Setup (Core Project)

**Goal**: Update Core project to support both logging patterns during transition

**Steps:**
1. Add `Microsoft.Extensions.Logging.Abstractions` to Core project
2. Update `LoggerFactory` class to provide MEL logger instances
3. Keep existing Serilog infrastructure for backward compatibility

**Core Changes:**
```csharp
// DropBear.Codex.Core/Logging/LoggerFactory.cs

using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

public static class LoggerFactory
{
    // Existing Serilog method (keep for backward compatibility)
    public static ILogger Logger => Log.Logger;

    // New MEL method
    private static ILoggerFactory? _loggerFactory;

    public static void ConfigureLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        if (_loggerFactory == null)
        {
            throw new InvalidOperationException(
                "LoggerFactory not configured. Call ConfigureLoggerFactory() first.");
        }
        return _loggerFactory.CreateLogger<T>();
    }
}
```

### Phase 2: Pilot Migration (Serialization Project)

**Goal**: Migrate JsonSerializer.cs as proof of concept

**Why JsonSerializer:**
- High-frequency logging (17 log statements)
- Clear, well-defined logging patterns
- Representative of typical usage

**Migration Pattern:**

**Before (Serilog):**
```csharp
public sealed class JsonSerializer : ISerializer
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializer>();

    public void SomeMethod()
    {
        _logger.Information("Starting operation for {Type}", typeName);
    }
}
```

**After (MEL + LoggerMessage):**
```csharp
public sealed partial class JsonSerializer : ISerializer
{
    private readonly Microsoft.Extensions.Logging.ILogger<JsonSerializer> _logger;

    public JsonSerializer(
        SerializationConfig config,
        Microsoft.Extensions.Logging.ILogger<JsonSerializer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // ... rest of constructor
    }

    // LoggerMessage source generators
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting operation for {Type}")]
    partial void LogOperationStarting(string type);

    public void SomeMethod()
    {
        LogOperationStarting(typeName);
    }
}
```

**Key Changes:**
1. Class becomes `partial`
2. Logger injected via constructor
3. Traditional log calls replaced with LoggerMessage methods
4. Add `#region LoggerMessage Source Generators` at end of file

### Phase 3: Dependency Injection Setup

**Goal**: Configure DI containers to provide MEL loggers

**For Library Users:**
```csharp
// In their Startup.cs or Program.cs
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSerilog(); // Serilog as the sink
});

builder.Services.AddDropBearCodex();
```

**For Test Projects:**
```csharp
var services = new ServiceCollection();
services.AddLogging(logging =>
{
    logging.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger());
});
```

### Phase 4: Gradual Migration

**Migration Order (by priority):**

1. **High-frequency files first:**
   - ✅ WorkflowEngine.cs (already done)
   - JsonSerializer.cs (17 logs)
   - MessagePackSerializer.cs (12 logs)
   - ExecutionEngine.cs (12 logs)
   - FileManager.cs (45 logs)

2. **By project:**
   - Serialization (223 logs, 24 files)
   - Files (128 logs, 8 files)
   - Tasks (88 logs, 11 files)
   - Blazor (67 logs, 5 files)

3. **Low-frequency files last**

**Per-File Checklist:**
- [ ] Make class `partial`
- [ ] Change logger field from `Serilog.ILogger` to `Microsoft.Extensions.Logging.ILogger<T>`
- [ ] Update constructor to inject logger
- [ ] Convert log statements to LoggerMessage methods
- [ ] Add `#region LoggerMessage Source Generators`
- [ ] Build and test
- [ ] Update tests to provide MEL logger

### Phase 5: Breaking Change Management

**Backward Compatibility:**
- Keep `LoggerFactory.Logger` (Serilog) available during transition
- Mark as `[Obsolete]` with migration guidance
- Remove in next major version

**Version Strategy:**
- Current: v2025.10.x (pre-migration)
- Migration: v2025.11.x (breaking change, requires MEL setup)
- Document breaking changes in release notes

## Implementation Details

### Package References Required

**All Projects:**
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

**Consumer Applications (not library):**
```xml
<PackageReference Include="Serilog.Extensions.Logging" />
```

### Constructor Injection Pattern

**For classes with existing constructor:**
```csharp
// Before
public MyClass(SomeService service)
{
    _service = service;
    _logger = LoggerFactory.Logger.ForContext<MyClass>();
}

// After
public MyClass(
    SomeService service,
    ILogger<MyClass> logger)
{
    _service = service;
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

**For classes with no constructor (rare):**
```csharp
// Add new constructor
public MyClass(ILogger<MyClass> logger)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

### LoggerMessage Patterns

**Information Logs:**
```csharp
[LoggerMessage(
    Level = LogLevel.Information,
    Message = "Operation completed for {Type} in {ElapsedMs}ms")]
partial void LogOperationCompleted(string type, long elapsedMs);
```

**Warning Logs:**
```csharp
[LoggerMessage(
    Level = LogLevel.Warning,
    Message = "Attempted to process null value of type {Type}")]
partial void LogNullValue(string type);
```

**Error Logs with Exception:**
```csharp
[LoggerMessage(
    Level = LogLevel.Error,
    Message = "Operation failed for type {Type}")]
partial void LogOperationError(string type, Exception ex);
```

**Naming Convention:**
- Start with `Log`
- Describe the event (not the action)
- Use present tense
- Example: `LogSerializationStarting` not `StartSerialization`

### Testing Considerations

**Unit Tests:**
```csharp
// Use NSubstitute or Moq
var logger = Substitute.For<ILogger<JsonSerializer>>();
var serializer = new JsonSerializer(config, logger);

// Verify logging
logger.Received(1).Log(
    LogLevel.Information,
    Arg.Any<EventId>(),
    Arg.Is<object>(o => o.ToString()!.Contains("expected message")),
    null,
    Arg.Any<Func<object, Exception?, string>>());
```

**Integration Tests:**
```csharp
var services = new ServiceCollection();
services.AddLogging(logging => logging.AddSerilog(testLogger));
services.AddYourServices();
var provider = services.BuildServiceProvider();

var service = provider.GetRequiredService<IYourService>();
```

## Timeline Estimate

Based on 506 log statements across 49 files:

- **Phase 1** (Infrastructure): 2-4 hours
- **Phase 2** (Pilot): 1-2 hours
- **Phase 3** (DI Setup): 1 hour
- **Phase 4** (Gradual Migration):
  - Per file: 15-30 minutes
  - 49 files × 20 min avg = 16 hours
- **Phase 5** (Testing & Documentation): 4 hours

**Total Estimate**: 24-27 hours over 3-4 weeks (gradual rollout)

## Risk Mitigation

**Risk 1**: Breaking changes for library consumers
- **Mitigation**: Clear migration guide, obsolete warnings, major version bump

**Risk 2**: Performance regression during transition
- **Mitigation**: Migrate high-frequency paths first, benchmark before/after

**Risk 3**: Test failures due to logger mocking
- **Mitigation**: Update test utilities, provide test helpers

**Risk 4**: Complex DI setup for consumers
- **Mitigation**: Document common scenarios, provide extension methods

## Success Criteria

- [ ] All 506 log statements converted to LoggerMessage
- [ ] 0 build errors
- [ ] All tests passing
- [ ] Benchmark shows 20-40% logging performance improvement
- [ ] Documentation updated
- [ ] Migration guide published

## Next Steps

1. Review and approve this strategy
2. Create feature branch: `feature/logging-migration`
3. Implement Phase 1 (Infrastructure)
4. Implement Phase 2 (Pilot: JsonSerializer.cs)
5. Validate pilot works end-to-end
6. Proceed with gradual rollout

---

**Status**: Draft - Awaiting Approval
**Last Updated**: 2025-11-01
**Est. Completion**: 2025-11-24 (3-4 weeks)
