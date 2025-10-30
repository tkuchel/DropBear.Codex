# Security Headers Middleware

## Overview

The `SecurityHeadersMiddleware` provides comprehensive security headers for Blazor applications following OWASP best practices. The middleware is fully configurable and supports both development and production environments.

## Features

- ✅ **Content Security Policy (CSP)**: Prevents XSS, clickjacking, and code injection
- ✅ **HTTP Strict Transport Security (HSTS)**: Forces HTTPS connections
- ✅ **X-Frame-Options**: Prevents clickjacking attacks
- ✅ **X-Content-Type-Options**: Prevents MIME sniffing
- ✅ **Referrer Policy**: Controls referrer information leakage
- ✅ **Permissions Policy**: Restricts browser API access
- ✅ **Cross-Origin Policies**: Modern isolation for SharedArrayBuffer and similar features
- ✅ **Nonce-based CSP**: Optional per-request nonce generation for inline scripts

## Quick Start

### Basic Usage (Production Defaults)

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register security headers with production defaults
builder.Services.AddSecurityHeaders();

var app = builder.Build();

// Apply security headers middleware (should be early in pipeline)
app.UseSecurityHeaders();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

### Development Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSecurityHeaders(options =>
    {
        // Use permissive CSP for development
        options.ContentSecurityPolicy = ContentSecurityPolicyHelper.Presets.Development;
        options.AddHsts = false; // Don't enforce HTTPS in development
        options.AddCrossOriginPolicies = false;
    });
}
else
{
    builder.Services.AddSecurityHeaders(); // Production defaults
}
```

### Custom Configuration

```csharp
builder.Services.AddSecurityHeaders(options =>
{
    // Enable strict CSP with custom sources
    options.UseStrictCsp = true;
    options.AllowedScriptSources.Add("https://cdn.example.com");
    options.AllowedStyleSources.Add("https://fonts.googleapis.com");

    // Enable nonce-based CSP for inline scripts
    options.UseNonceCsp = true;

    // Configure HSTS
    options.AddHsts = true;
    options.HstsMaxAge = 63072000; // 2 years
    options.HstsIncludeSubDomains = true;
    options.HstsPreload = true;

    // Enable cross-origin isolation
    options.AddCrossOriginPolicies = true;
    options.CrossOriginOpenerPolicy = "same-origin";
    options.CrossOriginEmbedderPolicy = "require-corp";

    // Custom frame options
    options.XFrameOptions = "SAMEORIGIN"; // Allow framing from same origin

    // Custom referrer policy
    options.ReferrerPolicy = "no-referrer-when-downgrade";
});
```

## Configuration Options

### SecurityHeadersOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ContentSecurityPolicy` | `string?` | `null` | Custom CSP policy (auto-generated if null) |
| `UseStrictCsp` | `bool` | `true` | Use strict Blazor Server CSP when CSP is null |
| `UseNonceCsp` | `bool` | `false` | Generate per-request nonce for inline scripts |
| `XFrameOptions` | `string` | `"DENY"` | X-Frame-Options header value |
| `ReferrerPolicy` | `string` | `"strict-origin-when-cross-origin"` | Referrer-Policy header value |
| `PermissionsPolicy` | `string` | Restrictive | Permissions-Policy header value |
| `AddXContentTypeOptions` | `bool` | `true` | Add X-Content-Type-Options: nosniff |
| `AddXssProtection` | `bool` | `true` | Add X-XSS-Protection header |
| `AddHsts` | `bool` | `true` | Add HSTS header when HTTPS |
| `HstsMaxAge` | `int` | `31536000` | HSTS max-age in seconds (1 year default) |
| `HstsIncludeSubDomains` | `bool` | `true` | Include subdomains in HSTS |
| `HstsPreload` | `bool` | `true` | Enable HSTS preload |
| `DisableDnsPrefetch` | `bool` | `true` | Add X-DNS-Prefetch-Control: off |
| `AddCrossOriginPolicies` | `bool` | `false` | Add COEP, COOP, CORP headers |
| `CrossOriginEmbedderPolicy` | `string` | `"require-corp"` | COEP header value |
| `CrossOriginOpenerPolicy` | `string` | `"same-origin"` | COOP header value |
| `CrossOriginResourcePolicy` | `string` | `"same-origin"` | CORP header value |
| `AllowedScriptSources` | `List<string>` | Empty | Additional script sources for CSP |
| `AllowedStyleSources` | `List<string>` | Empty | Additional style sources for CSP |
| `SignalREndpoint` | `string?` | `null` | SignalR endpoint for Blazor Server CSP |

## Presets

### Production
```csharp
var options = SecurityHeadersOptions.Production;
// - Strict CSP enabled
// - HSTS enabled (1 year, includeSubDomains, preload)
// - Cross-origin policies enabled
```

### Development
```csharp
var options = SecurityHeadersOptions.Development;
// - Permissive CSP for hot-reload
// - HSTS disabled
// - Cross-origin policies disabled
```

## Using Nonce-based CSP

When `UseNonceCsp` is enabled, a cryptographically secure nonce is generated for each request and made available via `HttpContext.Items["CSP-Nonce"]`.

### Accessing Nonce in Razor Components

```razor
@inject IHttpContextAccessor HttpContextAccessor

<script nonce="@GetNonce()">
    console.log('This inline script is allowed by CSP');
</script>

@code {
    private string? GetNonce()
    {
        return HttpContextAccessor.HttpContext?.Items["CSP-Nonce"] as string;
    }
}
```

### Accessing Nonce in _Host.cshtml

```html
@{
    var nonce = Context.Items["CSP-Nonce"] as string;
}

<script nonce="@nonce">
    // Inline script allowed by nonce
</script>
```

## Content Security Policy Helper

The middleware uses `ContentSecurityPolicyHelper` for CSP generation:

```csharp
// Generate Blazor Server policy
var csp = ContentSecurityPolicyHelper.GenerateBlazorServerPolicy(
    allowedScriptSources: new[] { "https://cdn.example.com" },
    allowedStyleSources: new[] { "https://fonts.googleapis.com" },
    signalREndpoint: "wss://myapp.example.com",
    nonce: "randomly-generated-nonce"
);

// Generate Blazor WASM policy
var csp = ContentSecurityPolicyHelper.GenerateBlazorWasmPolicy(
    allowedScriptSources: new[] { "https://cdn.example.com" }
);

// Use presets
var csp = ContentSecurityPolicyHelper.Presets.StrictBlazorServer;
var devCsp = ContentSecurityPolicyHelper.Presets.Development;

// Build custom CSP
var csp = ContentSecurityPolicyHelper.CreateBuilder()
    .WithDefaultSrc("'self'")
    .WithScriptSrc("'self'", "'wasm-unsafe-eval'")
    .WithStyleSrc("'self'", "'unsafe-inline'")
    .Build();
```

## Cross-Origin Policies

Cross-origin policies provide process isolation for modern web features like SharedArrayBuffer:

### Cross-Origin-Embedder-Policy (COEP)
- `require-corp`: Requires CORS or CORP for cross-origin resources
- `credentialless`: Loads cross-origin resources without credentials

### Cross-Origin-Opener-Policy (COOP)
- `same-origin`: Isolates browsing context to same origin
- `same-origin-allow-popups`: Allows popups but maintains isolation
- `unsafe-none`: No isolation (default behavior)

### Cross-Origin-Resource-Policy (CORP)
- `same-origin`: Only same-origin requests can load resource
- `same-site`: Same-site requests can load resource
- `cross-origin`: Any origin can load resource

**Note**: These headers may break embedded iframes, third-party scripts, or cross-origin assets. Test thoroughly before enabling in production.

## Security Best Practices

### Production Checklist

- ✅ Enable strict CSP (`UseStrictCsp = true`)
- ✅ Enable HSTS with long max-age (1+ years)
- ✅ Use `X-Frame-Options: DENY` unless iframes are required
- ✅ Keep `X-Content-Type-Options: nosniff` enabled
- ✅ Use restrictive Permissions-Policy
- ✅ Consider enabling cross-origin policies if using SharedArrayBuffer
- ✅ Use nonce-based CSP for inline scripts instead of `unsafe-inline`
- ✅ Regularly audit allowed sources in CSP
- ✅ Test headers with security tools (e.g., securityheaders.com)

### HSTS Preload Submission

To submit your site to the HSTS preload list:

1. Enable HSTS with:
   - `max-age >= 31536000` (1 year)
   - `includeSubDomains`
   - `preload`
2. Redirect HTTP to HTTPS at base domain
3. Serve HSTS header on base domain
4. Submit at https://hstspreload.org

**Warning**: HSTS preload is difficult to undo. Test thoroughly first.

## Testing Security Headers

### Browser Developer Tools

```javascript
// Check headers in browser console
fetch('/')
  .then(response => {
    console.log('CSP:', response.headers.get('content-security-policy'));
    console.log('HSTS:', response.headers.get('strict-transport-security'));
  });
```

### Online Tools

- https://securityheaders.com - Comprehensive header analysis
- https://csp-evaluator.withgoogle.com - CSP policy evaluation
- https://observatory.mozilla.org - Mozilla security observatory

### curl Command

```bash
curl -I https://your-app.com

# Expected output:
# Content-Security-Policy: default-src 'self'; ...
# Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
# X-Frame-Options: DENY
# X-Content-Type-Options: nosniff
# ...
```

## Troubleshooting

### CSP Violations

If you see CSP violations in the browser console:

1. Check which resource is being blocked
2. Add the domain to `AllowedScriptSources` or `AllowedStyleSources`
3. For development, temporarily use `ContentSecurityPolicyHelper.Presets.Development`
4. Consider using nonce-based CSP for inline scripts

### HSTS Issues

If HSTS causes localhost issues:

1. Clear HSTS settings: `chrome://net-internals/#hsts` (Chrome)
2. Use different domains for local dev (not localhost)
3. Disable HSTS in development: `options.AddHsts = false`

### Cross-Origin Policy Breaks Features

If enabling cross-origin policies breaks functionality:

1. Ensure all cross-origin resources have proper CORS headers
2. Add `crossorigin="anonymous"` to script/link tags
3. Serve CORP headers on cross-origin resources
4. Consider using `credentialless` COEP instead of `require-corp`

## Additional Resources

- [OWASP Security Headers](https://owasp.org/www-project-secure-headers/)
- [Content Security Policy Reference](https://content-security-policy.com/)
- [MDN Web Security](https://developer.mozilla.org/en-US/docs/Web/Security)
- [DropBear.Codex Documentation](https://github.com/tkuchel/DropBear.Codex)
