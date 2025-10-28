using Microsoft.AspNetCore.Http;

namespace DropBear.Codex.Blazor.Middleware;

/// <summary>
///     Middleware that adds security-related HTTP headers to all responses.
///     Implements OWASP best practices for web application security.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SecurityHeadersMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    ///     Invokes the middleware to add security headers to the response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Add security headers before processing the request
        AddSecurityHeaders(context.Response);

        // Call the next middleware in the pipeline
        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds comprehensive security headers to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to add headers to.</param>
    private static void AddSecurityHeaders(HttpResponse response)
    {
        var headers = response.Headers;

        // Prevent clickjacking attacks
        // Restricts the page from being displayed in frames/iframes
        headers.Append("X-Frame-Options", "DENY");

        // Alternative to X-Frame-Options with more granular control
        headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Blazor requires unsafe-eval
            "style-src 'self' 'unsafe-inline'; " + // Allow inline styles for Blazor
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'");

        // Prevent MIME type sniffing
        // Ensures browsers respect the declared Content-Type
        headers.Append("X-Content-Type-Options", "nosniff");

        // Enable XSS protection in older browsers
        // Modern browsers use CSP, but this provides defense-in-depth
        headers.Append("X-XSS-Protection", "1; mode=block");

        // Control referrer information sent with requests
        // Prevents leaking sensitive URL information to third parties
        headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Feature Policy / Permissions Policy
        // Restrict access to browser features and APIs
        headers.Append("Permissions-Policy",
            "accelerometer=(), " +
            "camera=(), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "magnetometer=(), " +
            "microphone=(), " +
            "payment=(), " +
            "usb=()");

        // HTTP Strict Transport Security (HSTS)
        // Force HTTPS for 1 year, including subdomains
        // Note: Only add this if your application uses HTTPS
        if (response.HttpContext.Request.IsHttps)
        {
            headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
        }

        // Prevent DNS prefetching
        headers.Append("X-DNS-Prefetch-Control", "off");

        // Disable client-side caching for sensitive pages
        // Uncomment if needed for authentication pages
        // headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, private");
        // headers.Append("Pragma", "no-cache");
        // headers.Append("Expires", "0");
    }
}
