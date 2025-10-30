#region

using DropBear.Codex.Blazor.Middleware;
using DropBear.Codex.Blazor.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#endregion

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Extension methods for adding security headers middleware to the application pipeline.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    ///     Adds security headers configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddSecurityHeaders(
        this IServiceCollection services,
        Action<SecurityHeadersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // Use default production configuration if none provided
            services.Configure<SecurityHeadersOptions>(options =>
            {
                var production = SecurityHeadersOptions.Production;
                options.UseStrictCsp = production.UseStrictCsp;
                options.AddHsts = production.AddHsts;
                options.HstsMaxAge = production.HstsMaxAge;
                options.HstsIncludeSubDomains = production.HstsIncludeSubDomains;
                options.HstsPreload = production.HstsPreload;
                options.AddCrossOriginPolicies = production.AddCrossOriginPolicies;
            });
        }

        return services;
    }

    /// <summary>
    ///     Adds security headers middleware to the application pipeline with default production settings.
    ///     This middleware adds comprehensive security headers including CSP, HSTS, X-Frame-Options, etc.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <example>
    /// Usage in Program.cs:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// // Register security headers configuration
    /// builder.Services.AddSecurityHeaders();
    ///
    /// var app = builder.Build();
    ///
    /// // Add security headers middleware (should be early in pipeline)
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

        // Ensure options are registered
        var serviceProvider = builder.ApplicationServices;
        var options = serviceProvider.GetService<IOptions<SecurityHeadersOptions>>();
        if (options == null)
        {
            // Register default options if not already registered
            var services = new ServiceCollection();
            services.Configure<SecurityHeadersOptions>(_ => { });
        }

        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>
    ///     Adds security headers middleware to the application pipeline with custom configuration.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">Configuration action for security headers options.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <example>
    /// Usage in Program.cs:
    /// <code>
    /// var app = builder.Build();
    ///
    /// // Add security headers with custom configuration
    /// app.UseSecurityHeaders(options =>
    /// {
    ///     options.UseStrictCsp = true;
    ///     options.AddCrossOriginPolicies = true;
    ///     options.AllowedScriptSources.Add("https://cdn.example.com");
    /// });
    ///
    /// app.Run();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder builder,
        Action<SecurityHeadersOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Configure options
        var services = new ServiceCollection();
        services.Configure(configure);
        var serviceProvider = services.BuildServiceProvider();

        return builder.UseMiddleware<SecurityHeadersMiddleware>(
            Microsoft.Extensions.Options.Options.Create(
                serviceProvider.GetRequiredService<IOptions<SecurityHeadersOptions>>().Value));
    }
}
