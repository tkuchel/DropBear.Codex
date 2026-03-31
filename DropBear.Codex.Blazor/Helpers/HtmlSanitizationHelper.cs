using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
/// Provides HTML sanitization to prevent XSS (Cross-Site Scripting) attacks.
/// SECURITY: Always sanitize user-provided content before rendering as MarkupString.
/// </summary>
public static partial class HtmlSanitizationHelper
{
    /// <summary>
     /// Sanitizes untrusted HTML content by HTML-encoding it before rendering.
     /// SECURITY: This method intentionally favors safety over preserving formatting.
     /// </summary>
     /// <param name="html">The HTML content to sanitize</param>
     /// <returns>Sanitized HTML safe for rendering</returns>
    public static MarkupString Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new MarkupString(string.Empty);
        }

        return new MarkupString(Escape(html));
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
