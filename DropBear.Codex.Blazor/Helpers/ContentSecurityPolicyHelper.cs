#region

using System.Collections.Frozen;
using System.Text;

#endregion

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Provides utilities for generating and managing Content Security Policy (CSP) headers
///     to protect Blazor applications from XSS, clickjacking, and other code injection attacks.
/// </summary>
/// <remarks>
///     CSP is a critical security layer that helps detect and mitigate certain types of attacks,
///     including Cross-Site Scripting (XSS) and data injection attacks.
/// </remarks>
public static class ContentSecurityPolicyHelper
{
    /// <summary>
    ///     CSP directive names.
    /// </summary>
    private static class Directives
    {
        public const string DefaultSrc = "default-src";
        public const string ScriptSrc = "script-src";
        public const string StyleSrc = "style-src";
        public const string ImgSrc = "img-src";
        public const string FontSrc = "font-src";
        public const string ConnectSrc = "connect-src";
        public const string FrameSrc = "frame-src";
        public const string FrameAncestors = "frame-ancestors";
        public const string ObjectSrc = "object-src";
        public const string BaseUri = "base-uri";
        public const string FormAction = "form-action";
        public const string UpgradeInsecureRequests = "upgrade-insecure-requests";
        public const string BlockAllMixedContent = "block-all-mixed-content";
    }

    /// <summary>
    ///     CSP source keywords.
    /// </summary>
    private static class Sources
    {
        public const string Self = "'self'";
        public const string None = "'none'";
        public const string UnsafeInline = "'unsafe-inline'";
        public const string UnsafeEval = "'unsafe-eval'";
        public const string StrictDynamic = "'strict-dynamic'";
        public const string UnsafeHashes = "'unsafe-hashes'";
        public const string WasmUnsafeEval = "'wasm-unsafe-eval'";
    }

    /// <summary>
    ///     Generates a strict CSP policy suitable for Blazor WebAssembly applications.
    /// </summary>
    /// <param name="allowedScriptSources">Additional allowed script sources (domains).</param>
    /// <param name="allowedStyleSources">Additional allowed style sources (domains).</param>
    /// <param name="nonce">Optional nonce value for inline scripts/styles.</param>
    /// <returns>A CSP policy string ready to be used in HTTP headers or meta tags.</returns>
    /// <remarks>
    ///     This policy is designed for Blazor WebAssembly which requires 'unsafe-eval' for JavaScript interop.
    ///     For production, consider using nonces for inline scripts instead of 'unsafe-inline'.
    /// </remarks>
    public static string GenerateBlazorWasmPolicy(
        IEnumerable<string>? allowedScriptSources = null,
        IEnumerable<string>? allowedStyleSources = null,
        string? nonce = null)
    {
        var policy = new StringBuilder();

        // Default: restrict to same origin
        policy.Append($"{Directives.DefaultSrc} {Sources.Self}; ");

        // Script sources: Blazor WASM requires 'wasm-unsafe-eval' for WebAssembly
        policy.Append($"{Directives.ScriptSrc} {Sources.Self} {Sources.WasmUnsafeEval}");
        if (!string.IsNullOrEmpty(nonce))
        {
            policy.Append($" 'nonce-{nonce}'");
        }
        if (allowedScriptSources != null)
        {
            foreach (var source in allowedScriptSources)
            {
                policy.Append($" {source}");
            }
        }
        policy.Append("; ");

        // Style sources: Allow inline styles for Blazor components
        policy.Append($"{Directives.StyleSrc} {Sources.Self} {Sources.UnsafeInline}");
        if (allowedStyleSources != null)
        {
            foreach (var source in allowedStyleSources)
            {
                policy.Append($" {source}");
            }
        }
        policy.Append("; ");

        // Images: Allow data URIs for embedded images
        policy.Append($"{Directives.ImgSrc} {Sources.Self} data: blob:; ");

        // Fonts: Restrict to same origin and data URIs
        policy.Append($"{Directives.FontSrc} {Sources.Self} data:; ");

        // AJAX/WebSocket connections
        policy.Append($"{Directives.ConnectSrc} {Sources.Self}; ");

        // Frames: Prevent clickjacking
        policy.Append($"{Directives.FrameAncestors} {Sources.None}; ");

        // Object/Embed: Block plugins
        policy.Append($"{Directives.ObjectSrc} {Sources.None}; ");

        // Base URI: Restrict to same origin
        policy.Append($"{Directives.BaseUri} {Sources.Self}; ");

        // Form actions: Restrict to same origin
        policy.Append($"{Directives.FormAction} {Sources.Self}; ");

        // Upgrade insecure requests (HTTP → HTTPS)
        policy.Append($"{Directives.UpgradeInsecureRequests}");

        return policy.ToString().TrimEnd();
    }

    /// <summary>
    ///     Generates a strict CSP policy suitable for Blazor Server applications.
    /// </summary>
    /// <param name="allowedScriptSources">Additional allowed script sources (domains).</param>
    /// <param name="allowedStyleSources">Additional allowed style sources (domains).</param>
    /// <param name="signalREndpoint">The SignalR endpoint for Blazor Server (default: self).</param>
    /// <param name="nonce">Optional nonce value for inline scripts/styles.</param>
    /// <returns>A CSP policy string ready to be used in HTTP headers or meta tags.</returns>
    public static string GenerateBlazorServerPolicy(
        IEnumerable<string>? allowedScriptSources = null,
        IEnumerable<string>? allowedStyleSources = null,
        string? signalREndpoint = null,
        string? nonce = null)
    {
        var policy = new StringBuilder();

        // Default: restrict to same origin
        policy.Append($"{Directives.DefaultSrc} {Sources.Self}; ");

        // Script sources: Blazor Server needs SignalR
        policy.Append($"{Directives.ScriptSrc} {Sources.Self}");
        if (!string.IsNullOrEmpty(nonce))
        {
            policy.Append($" 'nonce-{nonce}'");
        }
        if (allowedScriptSources != null)
        {
            foreach (var source in allowedScriptSources)
            {
                policy.Append($" {source}");
            }
        }
        policy.Append("; ");

        // Style sources
        policy.Append($"{Directives.StyleSrc} {Sources.Self} {Sources.UnsafeInline}");
        if (allowedStyleSources != null)
        {
            foreach (var source in allowedStyleSources)
            {
                policy.Append($" {source}");
            }
        }
        policy.Append("; ");

        // Images
        policy.Append($"{Directives.ImgSrc} {Sources.Self} data: blob:; ");

        // Fonts
        policy.Append($"{Directives.FontSrc} {Sources.Self} data:; ");

        // Connect (SignalR WebSocket connections)
        policy.Append($"{Directives.ConnectSrc} {Sources.Self}");
        if (!string.IsNullOrEmpty(signalREndpoint))
        {
            policy.Append($" {signalREndpoint}");
        }
        policy.Append("; ");

        // Frames: Prevent clickjacking
        policy.Append($"{Directives.FrameAncestors} {Sources.None}; ");

        // Object/Embed
        policy.Append($"{Directives.ObjectSrc} {Sources.None}; ");

        // Base URI
        policy.Append($"{Directives.BaseUri} {Sources.Self}; ");

        // Form actions
        policy.Append($"{Directives.FormAction} {Sources.Self}; ");

        // Upgrade insecure requests
        policy.Append($"{Directives.UpgradeInsecureRequests}");

        return policy.ToString().TrimEnd();
    }

    /// <summary>
    ///     Generates a cryptographically secure nonce for CSP.
    /// </summary>
    /// <returns>A base64-encoded nonce string.</returns>
    /// <remarks>
    ///     Use this nonce in your CSP header and add it to inline scripts/styles:
    ///     &lt;script nonce="[nonce]"&gt;...&lt;/script&gt;
    /// </remarks>
    public static string GenerateNonce()
    {
        Span<byte> nonceBytes = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonceBytes);
        return Convert.ToBase64String(nonceBytes);
    }

    /// <summary>
    ///     Common CSP policy presets optimized for Blazor applications.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        ///     Strict policy for production Blazor WebAssembly apps.
        ///     Blocks all inline scripts/styles except those with nonces.
        /// </summary>
        public static string StrictBlazorWasm => GenerateBlazorWasmPolicy();

        /// <summary>
        ///     Strict policy for production Blazor Server apps.
        /// </summary>
        public static string StrictBlazorServer => GenerateBlazorServerPolicy();

        /// <summary>
        ///     Development-friendly policy allowing more permissive rules.
        ///     WARNING: Do not use in production!
        /// </summary>
        public static string Development =>
            $"{Directives.DefaultSrc} {Sources.Self}; " +
            $"{Directives.ScriptSrc} {Sources.Self} {Sources.UnsafeInline} {Sources.UnsafeEval} {Sources.WasmUnsafeEval}; " +
            $"{Directives.StyleSrc} {Sources.Self} {Sources.UnsafeInline}; " +
            $"{Directives.ImgSrc} {Sources.Self} data: blob: https:; " +
            $"{Directives.FontSrc} {Sources.Self} data:; " +
            $"{Directives.ConnectSrc} {Sources.Self} wss: ws:; " +
            $"{Directives.FrameAncestors} {Sources.None}";
    }

    /// <summary>
    ///     Validates a CSP policy string format.
    /// </summary>
    /// <param name="policy">The CSP policy to validate.</param>
    /// <returns>True if the policy appears valid; otherwise false.</returns>
    public static bool ValidatePolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            return false;
        }

        // Basic validation: check for known directive patterns
        var knownDirectives = FrozenSet.ToFrozenSet(new[]
        {
            Directives.DefaultSrc,
            Directives.ScriptSrc,
            Directives.StyleSrc,
            Directives.ImgSrc,
            Directives.FontSrc,
            Directives.ConnectSrc,
            Directives.FrameSrc,
            Directives.FrameAncestors,
            Directives.ObjectSrc,
            Directives.BaseUri,
            Directives.FormAction
        }, StringComparer.OrdinalIgnoreCase);

        return knownDirectives.Any(directive => policy.Contains(directive, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Creates a CSP policy builder for custom configurations.
    /// </summary>
    /// <returns>A new CSP policy builder instance.</returns>
    public static CspPolicyBuilder CreateBuilder() => new();

    /// <summary>
    ///     Builder for creating custom CSP policies with fluent API.
    /// </summary>
    public sealed class CspPolicyBuilder
    {
        private readonly Dictionary<string, List<string>> _directives = new(StringComparer.Ordinal);

        /// <summary>
        ///     Adds sources to a directive.
        /// </summary>
        public CspPolicyBuilder AddDirective(string directive, params string[] sources)
        {
            if (!_directives.ContainsKey(directive))
            {
                _directives[directive] = new List<string>();
            }

            _directives[directive].AddRange(sources);
            return this;
        }

        /// <summary>
        ///     Sets default-src directive.
        /// </summary>
        public CspPolicyBuilder WithDefaultSrc(params string[] sources) =>
            AddDirective(Directives.DefaultSrc, sources);

        /// <summary>
        ///     Sets script-src directive.
        /// </summary>
        public CspPolicyBuilder WithScriptSrc(params string[] sources) =>
            AddDirective(Directives.ScriptSrc, sources);

        /// <summary>
        ///     Sets style-src directive.
        /// </summary>
        public CspPolicyBuilder WithStyleSrc(params string[] sources) =>
            AddDirective(Directives.StyleSrc, sources);

        /// <summary>
        ///     Builds the final CSP policy string.
        /// </summary>
        public string Build()
        {
            var policy = new StringBuilder();

            foreach (var (directive, sources) in _directives)
            {
                policy.Append($"{directive} {string.Join(" ", sources)}; ");
            }

            return policy.ToString().TrimEnd();
        }
    }
}
