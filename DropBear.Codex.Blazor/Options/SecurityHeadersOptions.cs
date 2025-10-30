#region

using DropBear.Codex.Blazor.Helpers;

#endregion

namespace DropBear.Codex.Blazor.Options;

/// <summary>
///     Configuration options for security headers middleware.
/// </summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>
    ///     Gets or sets the Content-Security-Policy value.
    ///     If null, uses the default policy from ContentSecurityPolicyHelper.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; }

    /// <summary>
    ///     Gets or sets whether to use strict CSP for Blazor Server.
    ///     Default is true.
    /// </summary>
    public bool UseStrictCsp { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to use nonce-based CSP for inline scripts/styles.
    ///     When enabled, a nonce is generated per request and made available via HttpContext.
    /// </summary>
    public bool UseNonceCsp { get; set; }

    /// <summary>
    ///     Gets or sets the X-Frame-Options value.
    ///     Common values: DENY, SAMEORIGIN
    ///     Default is DENY.
    /// </summary>
    public string XFrameOptions { get; set; } = "DENY";

    /// <summary>
    ///     Gets or sets the Referrer-Policy value.
    ///     Default is strict-origin-when-cross-origin.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    ///     Gets or sets the Permissions-Policy value.
    ///     Controls browser feature access.
    /// </summary>
    public string PermissionsPolicy { get; set; } =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    /// <summary>
    ///     Gets or sets whether to add X-Content-Type-Options: nosniff header.
    ///     Default is true.
    /// </summary>
    public bool AddXContentTypeOptions { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to add X-XSS-Protection header (for older browsers).
    ///     Default is true.
    /// </summary>
    public bool AddXssProtection { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to add HSTS header when request is HTTPS.
    ///     Default is true.
    /// </summary>
    public bool AddHsts { get; set; } = true;

    /// <summary>
    ///     Gets or sets the HSTS max-age in seconds.
    ///     Default is 31536000 (1 year).
    /// </summary>
    public int HstsMaxAge { get; set; } = 31536000;

    /// <summary>
    ///     Gets or sets whether HSTS should include subdomains.
    ///     Default is true.
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether HSTS preload should be enabled.
    ///     Default is true.
    /// </summary>
    public bool HstsPreload { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to add X-DNS-Prefetch-Control: off header.
    ///     Default is true.
    /// </summary>
    public bool DisableDnsPrefetch { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to add Cross-Origin policies.
    ///     Default is false (opt-in for compatibility).
    /// </summary>
    public bool AddCrossOriginPolicies { get; set; }

    /// <summary>
    ///     Gets or sets the Cross-Origin-Embedder-Policy value.
    ///     Common values: require-corp, credentialless
    ///     Only added if AddCrossOriginPolicies is true.
    /// </summary>
    public string CrossOriginEmbedderPolicy { get; set; } = "require-corp";

    /// <summary>
    ///     Gets or sets the Cross-Origin-Opener-Policy value.
    ///     Common values: same-origin, same-origin-allow-popups, unsafe-none
    ///     Only added if AddCrossOriginPolicies is true.
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>
    ///     Gets or sets the Cross-Origin-Resource-Policy value.
    ///     Common values: same-origin, same-site, cross-origin
    ///     Only added if AddCrossOriginPolicies is true.
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";

    /// <summary>
    ///     Gets or sets additional allowed script sources for CSP.
    ///     Only used if ContentSecurityPolicy is null.
    /// </summary>
    public List<string> AllowedScriptSources { get; set; } = new();

    /// <summary>
    ///     Gets or sets additional allowed style sources for CSP.
    ///     Only used if ContentSecurityPolicy is null.
    /// </summary>
    public List<string> AllowedStyleSources { get; set; } = new();

    /// <summary>
    ///     Gets or sets the SignalR endpoint for Blazor Server CSP.
    ///     Only used if ContentSecurityPolicy is null and UseStrictCsp is true.
    /// </summary>
    public string? SignalREndpoint { get; set; }

    /// <summary>
    ///     Creates default options for production environments.
    /// </summary>
    public static SecurityHeadersOptions Production => new()
    {
        UseStrictCsp = true,
        AddHsts = true,
        HstsMaxAge = 31536000,
        HstsIncludeSubDomains = true,
        HstsPreload = true,
        AddCrossOriginPolicies = true
    };

    /// <summary>
    ///     Creates default options for development environments.
    /// </summary>
    public static SecurityHeadersOptions Development => new()
    {
        UseStrictCsp = false,
        ContentSecurityPolicy = ContentSecurityPolicyHelper.Presets.Development,
        AddHsts = false,
        AddCrossOriginPolicies = false
    };
}
