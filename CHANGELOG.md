# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
- **CI/CD Pipeline Enhancements** (Session 4 - 2025-10-28):
  - Updated .NET version from 8.0.x to 9.0.x across all workflows
  - Added Trivy security scanning for CRITICAL and HIGH severity vulnerabilities
  - Enabled code style enforcement in builds (`/p:EnforceCodeStyleInBuild=true`)
  - Completed project matrix with all 10 projects (added Notifications and Workflow)
- `DropBear.Codex.Serialization`: Updated `JsonSerializer` to handle circular references.

### Fixed
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

