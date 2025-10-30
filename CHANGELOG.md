# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Workflow Engine Architecture Improvements** (Session 6 Phase 2 - 2025-10-29):
  - Created `IWorkflowTypeResolver` interface and `AppDomainWorkflowTypeResolver` implementation for workflow context type discovery
  - Created `IWorkflowStateCoordinator` interface and `DefaultWorkflowStateCoordinator` implementation for state persistence operations
  - Created `IWorkflowSignalHandler` interface and `DefaultWorkflowSignalHandler` implementation for workflow signal delivery
  - Added `WorkflowStateInfo` lightweight record for querying workflow state without full context deserialization
  - Separated concerns in `PersistentWorkflowEngine` (reduced from 907 LOC to ~400 LOC)
  - Type discovery now lazy-loaded via `Lazy<ConcurrentDictionary<string, Type>>` for better performance
  - Signal handling now properly validates signal names and workflow states
  - State coordinator uses reflection to handle unknown generic types at runtime
- **Workflow Engine Performance & Stability Improvements** (Session 6 Phase 1 - 2025-10-29):
  - Implemented degree of parallelism limiting in `ParallelNode` using `SemaphoreSlim`
  - Added `CircularExecutionTrace<T>` class for O(1) trace operations (replaces O(n) `RemoveRange`)
  - Created `CompensationFailure` record type for tracking Saga pattern rollback failures
  - Added `CompensationFailures` property to `WorkflowResult<TContext>`
  - Added `HasCompensationFailures` property for checking compensation status
  - All parallel workflows now respect `Environment.ProcessorCount` for throttling
  - Execution trace no longer silently truncates - uses circular buffer with 10,000 entry capacity
  - Compensation errors now properly collected and reported instead of being swallowed
- **Central Package Management** (Session 5 - 2025-10-29):
  - Implemented CPM with `Directory.Packages.props` for centralized package version management
  - All 87 package versions now managed in single location
  - Removed version attributes from all 20 .csproj files
  - Improved maintainability and consistency across solution
- **Workflow Project Analyzer Configuration** (Session 5 - 2025-10-29):
  - Created `GlobalSuppressions.cs` with justified analyzer suppressions
  - Created `.editorconfig` for path-specific analyzer rules
  - Documentation warnings temporarily suppressed with TODO comments
  - Example/demo code appropriately excluded from strict rules
- **Test Infrastructure** (Session 4 - 2025-10-28):
  - Created `DropBear.Codex.Core.Tests` with 61 comprehensive Result pattern tests
  - Integrated xUnit 2.9.2, FluentAssertions 8.8.0, and coverlet.collector 6.0.4
  - Tests cover Result<TError>, Result<T, TError>, and all error types (SimpleError, CodedError, OperationError)
  - 100% test pass rate with baseline coverage of 4.35%
- **Code Coverage Reporting** (Session 4 - 2025-10-28):
  - Integrated ReportGenerator for HTML coverage reports
  - Added dedicated test-coverage job to GitHub Actions workflow
  - Coverage reports uploaded as artifacts with 30-day retention
  - Coverage summary automatically displayed in workflow logs
- `DropBear.Codex.Hashing`: Added new `SHA256Hasher` implementation for secure hashing.
- `DropBear.Codex.Blazor`: Introduced `LongWaitProgressBar` component with customizable styles.
- `DropBear.Codex.Core`: Added `Result<T>` return type to standardize API responses across all libraries.

### Changed
- **Workflow Engine Refactoring** (Session 6 Phase 2 - 2025-10-29):
  - Refactored `PersistentWorkflowEngine` to extract specialized services following Single Responsibility Principle
  - `PersistentWorkflowEngine` now creates `DefaultWorkflowSignalHandler` internally with callback to avoid circular dependency
  - Removed ~500 lines of type discovery, state coordination, and signal handling code from main engine
  - Updated DI registration in `ServiceCollectionExtensions` to register new infrastructure services
  - All existing public APIs remain unchanged (non-breaking refactoring)
- **CI/CD Pipeline Improvements** (Session 5 - 2025-10-29):
  - Fixed cross-platform compatibility: added `shell: bash` for Unix commands on Windows runners
  - Switched from legacy Coverlet MSBuild properties to `--collect:"XPlat Code Coverage"`
  - Updated coverage report paths to match coverlet.collector output structure
  - Made artifact names unique per OS to prevent conflicts (`coverage-ubuntu-latest`, `coverage-windows-latest`)
  - Disabled redundant `dotnet-ci.yml` workflow (had fundamental design flaws)
  - All CI jobs now run successfully on both Ubuntu and Windows
- **CI/CD Pipeline Enhancements** (Session 4 - 2025-10-28):
  - Updated .NET version from 8.0.x to 9.0.x across all workflows
  - Added Trivy security scanning for CRITICAL and HIGH severity vulnerabilities
  - Enabled code style enforcement in builds (`/p:EnforceCodeStyleInBuild=true`)
  - Completed project matrix with all 10 projects (added Notifications and Workflow)
- `DropBear.Codex.Serialization`: Updated `JsonSerializer` to handle circular references.

### Fixed
- **DropBear.Codex.Workflow Performance & Robustness** (Session 6 - 2025-10-29):
  - Fixed unbounded parallel execution that could exhaust system resources with 10+ parallel branches
  - Fixed inefficient O(n) trace truncation using `RemoveRange(0, n)` that shifted all elements
  - Fixed silent compensation exception swallowing - errors now properly tracked and reported to caller
  - Updated `LogCompensationFailed` to accept general `Exception` instead of only `InvalidOperationException`
  - Parallel workflows no longer start all tasks simultaneously - properly throttled via semaphore
  - Workflow results now include full details of compensation failures during Saga rollback
  - Added telemetry tag for compensation failures count in OpenTelemetry activities
- **DropBear.Codex.Workflow Build Errors** (Session 5 - 2025-10-29):
  - Fixed nullable reference warnings using pattern matching in `StepNode.cs` and `InMemoryWorkflowStateRepository.cs`
  - Added standard exception constructors (CA1032 compliance) to all custom exception classes
  - Added `ConfigureAwait(false)` to async operations in `DelayNode.cs`
  - Workflow project now builds with 0 errors in Release configuration
- **DropBear.Codex.Workflow Exception Design** (Session 5 - 2025-10-29):
  - `WorkflowConfigurationException`: Added parameterless, message, and message+innerException constructors
  - `WorkflowExecutionException`: Added parameterless, message, and message+innerException constructors
  - `WorkflowStepTimeoutException`: Added parameterless, message, and message+innerException constructors
  - All exception classes now comply with .NET exception design guidelines (CA1032)
- **SECURITY - HIGH SEVERITY** (Session 5 - 2025-10-29): Fixed multiple security issues in `DropBear.Codex.Utilities.Obfuscation.Jumbler`
  - ⚠️ **BREAKING CHANGE**: Jumbler v03 format incompatible with older versions
  - **H1 FIXED**: Removed hardcoded default encryption key - `keyPhrase` parameter now required
  - **H2 FIXED**: Increased PBKDF2 iterations from 10,000 to 600,000 (OWASP 2023 recommendation)
  - **H3 FIXED**: Implemented random 32-byte salt generation per operation using `RandomNumberGenerator.GetBytes()`
  - **M1 FIXED**: Migrated key cache to `ConcurrentDictionary` for thread-safe operation
  - **Enhancement**: Version identifier updated to v03 to indicate security improvements
  - **Security**: All cryptographic operations now follow current best practices
- **SECURITY - MEDIUM SEVERITY** (Session 5 - 2025-10-29): Enhanced connection string validation in `DropBear.Codex.Notifications`
  - **H4 MITIGATED**: Added null/empty validation with exception on startup
  - **Enhancement**: Added warning when plain-text passwords detected in connection strings
  - **Documentation**: Added XML comments recommending Azure Key Vault, Managed Identity, or Windows Authentication
- **SECURITY - CRITICAL** (Session 4 - 2025-10-28): Fixed authentication bypass vulnerability in `DropBear.Codex.Blazor.Helpers.AntiForgeryHelper`
  - ⚠️ **BREAKING CHANGE**: Token format changed - all existing tokens are invalid
  - **Vulnerability**: `ValidateToken` method always returned success without actually validating tokens
  - **Fix**: Implemented proper cryptographic hash verification using SHA256
  - **Enhancement**: Added timestamp-based token expiration (default: 2 hours)
  - **Security**: Used `CryptographicOperations.FixedTimeEquals` to prevent timing attacks
  - **New Format**: `[timestamp(8)][salt(16)][random(32)][hash(32)]` bytes
  - **Binding**: Tokens now properly validate userId and optional sessionId
  - **Migration**: See "AntiForgeryHelper Migration Guide" section below
- `DropBear.Codex.Files`: Fixed an issue where file verification hashes were incorrectly calculated under certain conditions.
- `DropBear.Codex.Operation`: Corrected a bug in the advanced operation manager that caused recovery steps to be skipped.

### Deprecated
- `DropBear.Codex.Utilities`: Deprecated `LegacyStringHelper` in favor of the new `StringExtensions` class.

---

## Migration Guides

### AntiForgeryHelper Breaking Change (Session 4 - 2025-10-28)

**Impact**: All existing anti-forgery tokens are invalid and must be regenerated.

**Vulnerability Details**:
The previous implementation had a critical security flaw where `ValidateToken` always returned success without performing any actual validation. This rendered CSRF protection completely ineffective.

**What Changed**:
1. Token format now includes timestamp for expiration checking
2. Proper SHA256 hash validation implemented
3. Constant-time comparison prevents timing attacks
4. Tokens properly bind to userId and sessionId

**Migration Steps**:

```csharp
// BEFORE (vulnerable - tokens never actually validated)
var token = AntiForgeryHelper.GenerateToken(userId, sessionId);
var isValid = AntiForgeryHelper.ValidateToken(token, userId, sessionId);
// Result was always Success regardless of token validity!

// AFTER (secure - proper validation)
var token = AntiForgeryHelper.GenerateToken(userId, sessionId);
var result = AntiForgeryHelper.ValidateToken(token, userId, sessionId);

if (result.IsSuccess)
{
    // Token is cryptographically valid, not expired, and matches user
    ProcessRequest();
}
else
{
    // Token failed validation - reject request
    return Unauthorized(result.Error.Message);
}
```

**Deployment Checklist**:
1. ✅ Deploy updated code to all environments
2. ✅ Existing tokens will fail validation (expected and desired)
3. ✅ Users will automatically receive new tokens on next request
4. ✅ Consider logging failed validations to monitor transition
5. ✅ Optional: Show friendly "Session refreshed for security" message

**Token Expiration Configuration**:
```csharp
// Default: 2 hours
var result = AntiForgeryHelper.ValidateToken(token, userId);

// Custom expiration
var result = AntiForgeryHelper.ValidateToken(
    token,
    userId,
    sessionId,
    maxAge: TimeSpan.FromHours(1)
);

// Use predefined expirations
// Options.DefaultExpiration = 2 hours
// Options.ShortExpiration = 15 minutes
```

---

## [1.0.0] - 2024-08-24
### Added
- Initial release of `DropBear.Codex` libraries:
    - `DropBear.Codex.Core`
    - `DropBear.Codex.Encoding`
    - `DropBear.Codex.Files`
    - `DropBear.Codex.Hashing`
    - `DropBear.Codex.Operation`
    - `DropBear.Codex.Serialization`
    - `DropBear.Codex.StateManagement`
    - `DropBear.Codex.Utilities`
    - `DropBear.Codex.Validation`

### Fixed
- N/A (Initial release)

## [0.1.0] - 2024-07-01
### Added
- Setup initial structure for `DropBear.Codex` libraries.
- Created foundational projects and initial implementations.

