# Security Policy

## Supported Versions

We release patches for security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 2025.x  | :white_check_mark: |
| 2024.x  | :x:                |
| < 2024  | :x:                |

## Reporting a Vulnerability

We take the security of DropBear.Codex.Core seriously. If you have discovered a security vulnerability in our project, we appreciate your help in disclosing it to us responsibly.

### How to Report

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report them via email to: **[your-security-email@example.com]**

You can also use GitHub's private security vulnerability reporting feature:
1. Navigate to the repository
2. Click on "Security" tab
3. Click "Report a vulnerability"

### What to Include

Please include the following information in your report:

- **Type of vulnerability** (e.g., buffer overflow, SQL injection, XSS, etc.)
- **Full paths of source file(s)** related to the vulnerability
- **Location of the affected source code** (tag/branch/commit or direct URL)
- **Step-by-step instructions** to reproduce the issue
- **Proof-of-concept or exploit code** (if possible)
- **Impact of the vulnerability**, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Updates**: Every 5 business days
- **Fix Timeline**: Varies based on severity
    - Critical: 7 days
    - High: 14 days
    - Medium: 30 days
    - Low: 90 days

### What to Expect

1. **Acknowledgment**: We'll acknowledge receipt of your vulnerability report within 48 hours
2. **Investigation**: We'll investigate and validate the vulnerability
3. **Updates**: You'll receive regular updates on our progress
4. **Resolution**: We'll work on a fix and prepare a security advisory
5. **Disclosure**: We'll coordinate with you on the public disclosure timeline

### Bug Bounty

Currently, we do not have a paid bug bounty program. However, we deeply appreciate security researchers who help us improve the security of our project and will publicly acknowledge your contribution (unless you prefer to remain anonymous).

## Security Best Practices

When using DropBear.Codex.Core in your applications, please follow these security best practices:

### 1. Error Message Sanitization

Never expose sensitive information in error messages that might be logged or shown to users:

```csharp
// ❌ BAD - May expose sensitive data
var error = new ValidationError($"Invalid connection string: {connectionString}");

// ✅ GOOD - Sanitized message
var error = new ValidationError("Invalid connection string format");
```

### 2. Exception Handling

When using `Result.FromException`, be aware that exception messages may contain sensitive data:

```csharp
// For production, consider sanitizing exception messages
var result = Result<Data, ErrorType>.FromException(ex);

// Or provide custom message
var result = Result<Data, ErrorType>.FromException(ex, "Operation failed");
```

### 3. Telemetry Configuration

Be cautious about what data you track in telemetry:

```csharp
// ❌ BAD - May log sensitive data
telemetry.TrackResultCreated(state, typeof(User), userEmail);

// ✅ GOOD - No PII in telemetry
telemetry.TrackResultCreated(state, typeof(User), null);
```

### 4. Serialization

When serializing envelopes or results, ensure sensitive data is not included:

```csharp
// Use [JsonIgnore] or custom serialization for sensitive properties
public class SensitiveData
{
    public string PublicInfo { get; set; }
    
    [JsonIgnore]
    public string Password { get; set; }  // Never serialized
}
```

### 5. Logging

Configure logging to avoid capturing sensitive information:

```csharp
// Disable stack traces in production
var errorHandler = new DefaultResultErrorHandler(
    telemetry: telemetry,
    captureStackTraces: false  // Set to false in production
);
```

## Known Security Considerations

### 1. Exception Stack Traces

The `ResultError.SourceException` property may contain stack traces with file paths and internal implementation details. In production environments:

- Set `captureStackTraces: false` in error handlers
- Sanitize error messages before logging
- Don't expose full error details to end users

### 2. Telemetry Data

Telemetry may capture:
- Method names and call sites
- Result types and states
- Exception types and messages

Ensure your telemetry backend has appropriate access controls and data retention policies.

### 3. Serialized Data

When serializing `Envelope` or `Result` objects:
- Validate header values
- Sanitize payloads
- Use secure transport (HTTPS/TLS)
- Implement authentication/authorization

## Security Updates

Subscribe to security advisories:
- Watch this repository for security updates
- Enable GitHub security alerts
- Follow our release notes

## Vulnerability Disclosure Policy

We follow a coordinated vulnerability disclosure process:

1. **Private Disclosure**: Report received privately
2. **Validation**: We validate and develop a fix
3. **Advisory**: We create a security advisory
4. **Patch Release**: We release a patched version
5. **Public Disclosure**: We publicly disclose the vulnerability 30 days after patch release

## Contact

For security-related questions or concerns:
- **Email**: [your-security-email@example.com]
- **PGP Key**: [Link to PGP key if applicable]

## Acknowledgments

We thank the following security researchers for their responsible disclosure:

<!-- Security researchers will be listed here -->
- None yet - be the first!

---

**Last Updated**: October 12, 2025
