#region

using DropBear.Codex.Blazor.Helpers;
using DropBear.Codex.Blazor.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

#endregion

namespace DropBear.Codex.Blazor.Middleware;

/// <summary>
///     Middleware that adds security-related HTTP headers to all responses.
///     Implements OWASP best practices for web application security.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SecurityHeadersMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">Security headers configuration options.</param>
    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _options = options.Value;
    }

    /// <summary>
    ///     Invokes the middleware to add security headers to the response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Generate nonce if configured
        string? nonce = null;
        if (_options.UseNonceCsp)
        {
            nonce = ContentSecurityPolicyHelper.GenerateNonce();
            context.Items["CSP-Nonce"] = nonce;
        }

        // Add security headers before processing the request
        AddSecurityHeaders(context.Response, nonce);

        // Call the next middleware in the pipeline
        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds comprehensive security headers to the HTTP response based on configuration.
    /// </summary>
    /// <param name="response">The HTTP response to add headers to.</param>
    /// <param name="nonce">Optional nonce for CSP.</param>
    private void AddSecurityHeaders(HttpResponse response, string? nonce)
    {
        var headers = response.Headers;

        // X-Frame-Options: Prevent clickjacking attacks
        headers.Append("X-Frame-Options", _options.XFrameOptions);

        // Content-Security-Policy: Comprehensive content restrictions
        var cspPolicy = _options.ContentSecurityPolicy;
        if (string.IsNullOrEmpty(cspPolicy))
        {
            // Generate CSP based on configuration
            cspPolicy = _options.UseStrictCsp
                ? ContentSecurityPolicyHelper.GenerateBlazorServerPolicy(
                    _options.AllowedScriptSources,
                    _options.AllowedStyleSources,
                    _options.SignalREndpoint,
                    nonce)
                : ContentSecurityPolicyHelper.Presets.Development;
        }

        headers.Append("Content-Security-Policy", cspPolicy);

        // X-Content-Type-Options: Prevent MIME type sniffing
        if (_options.AddXContentTypeOptions)
        {
            headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-XSS-Protection: Enable XSS protection in older browsers (defense-in-depth)
        if (_options.AddXssProtection)
        {
            headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy: Control referrer information
        headers.Append("Referrer-Policy", _options.ReferrerPolicy);

        // Permissions-Policy: Restrict access to browser features and APIs
        if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
        {
            headers.Append("Permissions-Policy", _options.PermissionsPolicy);
        }

        // HSTS: Force HTTPS (only when request is already HTTPS)
        if (_options.AddHsts && response.HttpContext.Request.IsHttps)
        {
            var hstsValue = $"max-age={_options.HstsMaxAge}";
            if (_options.HstsIncludeSubDomains)
            {
                hstsValue += "; includeSubDomains";
            }

            if (_options.HstsPreload)
            {
                hstsValue += "; preload";
            }

            headers.Append("Strict-Transport-Security", hstsValue);
        }

        // X-DNS-Prefetch-Control: Prevent DNS prefetching
        if (_options.DisableDnsPrefetch)
        {
            headers.Append("X-DNS-Prefetch-Control", "off");
        }

        // Cross-Origin Policies (opt-in for compatibility)
        if (_options.AddCrossOriginPolicies)
        {
            // Cross-Origin-Embedder-Policy: Requires CORP for cross-origin resources
            headers.Append("Cross-Origin-Embedder-Policy", _options.CrossOriginEmbedderPolicy);

            // Cross-Origin-Opener-Policy: Isolate browsing context
            headers.Append("Cross-Origin-Opener-Policy", _options.CrossOriginOpenerPolicy);

            // Cross-Origin-Resource-Policy: Control resource loading
            headers.Append("Cross-Origin-Resource-Policy", _options.CrossOriginResourcePolicy);
        }
    }
}
