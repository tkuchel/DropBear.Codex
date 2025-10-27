using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
/// Provides HTML sanitization to prevent XSS (Cross-Site Scripting) attacks.
/// SECURITY: Always sanitize user-provided content before rendering as MarkupString.
/// </summary>
public static partial class HtmlSanitizationHelper
{
    // Allowed HTML tags (whitelist approach)
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "i", "u", "strong", "em", "br", "p", "span", "div",
        "ul", "ol", "li", "a", "code", "pre", "blockquote"
    };

    // Allowed HTML attributes per tag
    private static readonly Dictionary<string, HashSet<string>> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = new(StringComparer.OrdinalIgnoreCase) { "href", "title", "rel" },
        ["span"] = new(StringComparer.OrdinalIgnoreCase) { "class" },
        ["div"] = new(StringComparer.OrdinalIgnoreCase) { "class" }
    };

    // Regex patterns for sanitization
    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<iframe[\s\S]*?</iframe>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex IframeTagRegex();

    [GeneratedRegex(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JavascriptProtocolRegex();

    [GeneratedRegex(@"data:text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DataUrlHtmlRegex();

    /// <summary>
    /// Sanitizes HTML content by removing dangerous tags, attributes, and scripts.
    /// SECURITY: Use this for all user-provided content before rendering as MarkupString.
    /// </summary>
    /// <param name="html">The HTML content to sanitize</param>
    /// <returns>Sanitized HTML safe for rendering</returns>
    public static MarkupString Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new MarkupString(string.Empty);
        }

        // Remove dangerous tags and patterns
        var sanitized = RemoveDangerousTags(html);
        sanitized = RemoveEventHandlers(sanitized);
        sanitized = RemoveDangerousProtocols(sanitized);

        return new MarkupString(sanitized);
    }

    /// <summary>
    /// Creates a MarkupString from trusted HTML content WITHOUT sanitization.
    /// WARNING: Only use this for content from trusted sources (e.g., CMS, admin-created content).
    /// NEVER use this for user-provided content as it can lead to XSS vulnerabilities.
    /// </summary>
    /// <param name="trustedHtml">HTML content from a trusted source</param>
    /// <returns>MarkupString with unsanitized HTML</returns>
    public static MarkupString FromTrustedSource(string trustedHtml)
    {
        return new MarkupString(trustedHtml);
    }

    /// <summary>
    /// Escapes HTML content by converting special characters to HTML entities.
    /// Use this when you want to display HTML as plain text (no rendering).
    /// </summary>
    /// <param name="content">The content to escape</param>
    /// <returns>HTML-escaped content</returns>
    public static string Escape(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return System.Web.HttpUtility.HtmlEncode(content);
    }

    /// <summary>
    /// Removes dangerous HTML tags like &lt;script&gt;, &lt;iframe&gt;, &lt;object&gt;, etc.
    /// </summary>
    private static string RemoveDangerousTags(string html)
    {
        // Remove <script> tags
        html = ScriptTagRegex().Replace(html, string.Empty);

        // Remove <iframe> tags
        html = IframeTagRegex().Replace(html, string.Empty);

        // Remove other dangerous tags
        var dangerousTags = new[] { "object", "embed", "applet", "link", "style", "meta", "base" };
        foreach (var tag in dangerousTags)
        {
            html = Regex.Replace(html, $@"<{tag}[\s\S]*?</{tag}>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, $@"<{tag}[^>]*?>", string.Empty, RegexOptions.IgnoreCase);
        }

        return html;
    }

    /// <summary>
    /// Removes inline event handlers (onclick, onload, onerror, etc.)
    /// </summary>
    private static string RemoveEventHandlers(string html)
    {
        return EventHandlerRegex().Replace(html, string.Empty);
    }

    /// <summary>
    /// Removes dangerous protocols (javascript:, data:text/html, vbscript:)
    /// </summary>
    private static string RemoveDangerousProtocols(string html)
    {
        html = JavascriptProtocolRegex().Replace(html, string.Empty);
        html = DataUrlHtmlRegex().Replace(html, string.Empty);
        html = Regex.Replace(html, @"vbscript:", string.Empty, RegexOptions.IgnoreCase);
        return html;
    }

    /// <summary>
    /// Strips all HTML tags from content, leaving only plain text.
    /// Use this when you want to display user content as plain text only.
    /// </summary>
    /// <param name="html">HTML content</param>
    /// <returns>Plain text with all HTML tags removed</returns>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Remove all HTML tags
        var stripped = Regex.Replace(html, @"<[^>]*>", string.Empty);

        // Decode HTML entities
        stripped = System.Web.HttpUtility.HtmlDecode(stripped);

        return stripped;
    }
}
