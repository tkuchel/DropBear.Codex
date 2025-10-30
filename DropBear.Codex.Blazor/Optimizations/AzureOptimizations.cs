#region

using System.IO.Compression;
using System.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Blazor.Optimizations;

/// <summary>
///     Provides optimizations for running the application in Azure App Service.
/// </summary>
public static class AzureOptimizations
{
    /// <summary>
    ///     Configures optimal settings for Azure App Service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddAzureOptimizations(this IServiceCollection services)
    {
        // Optimize GC for server workloads
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Configure HTTP client factory with appropriate timeouts for Azure
        services.AddHttpClient("AzureOptimized", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // Azure has a default 230 second timeout
        });

        // Configure connection resiliency
        services.Configure<HubOptions>(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.MaximumReceiveMessageSize = 1 * 1024 * 1024; // 1MB
        });

        // Optimize Kestrel for Azure App Service
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30MB
            options.Limits.MaxConcurrentConnections = null; // Allow Azure to manage connections
            options.Limits.MaxConcurrentUpgradedConnections = null;
            options.Limits.MinRequestBodyDataRate = null; // Disable rate limiting in Azure
        });

        return services;
    }

    /// <summary>
    ///     Adds memory cache with optimized settings for Azure App Service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="sizeLimit">Optional size limit for the memory cache.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddAzureOptimizedMemoryCache(
        this IServiceCollection services, int? sizeLimit = null)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = sizeLimit;
            options.CompactionPercentage = 0.2; // 20% compaction when limit is reached
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        return services;
    }

    /// <summary>
    ///     Applies recommended compression settings for Azure App Service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddAzureOptimizedCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/octet-stream", "application/json"]);
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        return services;
    }
}

/// <summary>
///     Default MIME types for response compression.
/// </summary>
internal static class ResponseCompressionDefaults
{
    /// <summary>
    ///     Default MIME types to compress.
    /// </summary>
    public static readonly string[] MimeTypes =
    {
        "text/plain", "text/css", "text/html", "application/javascript", "text/javascript", "application/xml",
        "text/xml", "application/json", "text/json", "image/svg+xml"
    };
}
