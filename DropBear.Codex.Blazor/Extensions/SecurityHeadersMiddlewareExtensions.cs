using DropBear.Codex.Blazor.Middleware;
using Microsoft.AspNetCore.Builder;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Extension methods for adding security headers middleware to the application pipeline.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    ///     Adds security headers middleware to the application pipeline.
    ///     This middleware adds comprehensive security headers including CSP, HSTS, X-Frame-Options, etc.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <example>
    /// Usage in Program.cs or Startup.cs:
    /// <code>
    /// var app = builder.Build();
    ///
    /// // Add security headers (should be early in pipeline)
    /// app.UseSecurityHeaders();
    ///
    /// app.UseRouting();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapBlazorHub();
    /// app.MapFallbackToPage("/_Host");
    ///
    /// app.Run();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
