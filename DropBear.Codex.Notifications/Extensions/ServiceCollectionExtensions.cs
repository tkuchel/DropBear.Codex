#region

using System.Reflection;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Notifications.Data;
using DropBear.Codex.Notifications.Infrastructure;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Notifications.Repositories;
using DropBear.Codex.Notifications.Services;
using MessagePipe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#endregion

namespace DropBear.Codex.Notifications.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to configure notification services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Configures and registers the notification services along with required dependencies.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configureOptions">Optional configuration for notification service options.</param>
    /// <param name="configureMessagePipe">Optional configuration for MessagePipe options.</param>
    /// <returns>The IServiceCollection for chaining additional operations.</returns>
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        Action<NotificationServiceOptions>? configureOptions = null,
        Action<MessagePipeOptions>? configureMessagePipe = null)
    {
        // Configure MessagePipe
        services.ConfigureMessagePipe(configureMessagePipe);

        // Configure service options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register object pooling for improved performance
        services.AddSingleton<NotificationPool>();

        // Register the NotificationFactory using its interface
        services.AddSingleton<INotificationFactory, NotificationFactory>();

        // Register the NotificationService using its interface
        services.AddSingleton<INotificationService, NotificationService>();

        // Register telemetry if not already registered
        services.TryAddSingleton<IResultTelemetry, DefaultResultTelemetry>();

        // Register time provider for testability (added in .NET 8)
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    ///     Configures MessagePipe with provided or default options.
    /// </summary>
    /// <param name="services">The IServiceCollection.</param>
    /// <param name="configure">Optional configuration action for MessagePipeOptions.</param>
    /// <returns>The IServiceCollection for chaining additional operations.</returns>
    private static IServiceCollection ConfigureMessagePipe(
        this IServiceCollection services,
        Action<MessagePipeOptions>? configure = null)
    {
        services.AddMessagePipe(options =>
        {
            // Default configuration
            options.EnableCaptureStackTrace = true;
            options.EnableAutoRegistration = true;
            options.SetAutoRegistrationSearchAssemblies(
                Assembly.GetExecutingAssembly(),
                Assembly.GetCallingAssembly());

            // Apply external configuration if provided
            configure?.Invoke(options);
        });

        return services;
    }

    /// <summary>
    ///     Adds notification center services with database persistence.
    ///     SECURITY: Ensure connection strings are stored securely (Azure Key Vault, AWS Secrets Manager, etc.)
    ///     and not committed to source control.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if connection string is missing or invalid.</exception>
    public static IServiceCollection AddNotificationCenter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate connection string presence
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing or empty. " +
                "Ensure it is properly configured in appsettings.json or environment variables.");
        }

        // Security check: Warn if connection string appears to contain plain-text credentials
        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.Contains("Integrated Security=true", StringComparison.OrdinalIgnoreCase))
        {
            // Log warning but allow (user might be using managed identity or other auth)
            // In production, consider using Azure SQL Managed Identity or Windows Authentication
            Console.WriteLine(
                "WARNING: Connection string contains password. " +
                "Consider using Azure Key Vault, Managed Identity, or Windows Authentication for production.");
        }

        // Register core notification services (which you already have)
        services.AddNotificationServices();

        // Register the notification center services
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationCenterService, NotificationCenterService>();

        // Register the notification bridge as a singleton
        // It will listen for notifications and persist them
        services.AddSingleton<NotificationBridge>();

        // Register DB Context if using Entity Framework
        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseSqlServer(connectionString,
                b => b.MigrationsAssembly("DropBear.Codex.Notifications"));
        });

        return services;
    }
}
