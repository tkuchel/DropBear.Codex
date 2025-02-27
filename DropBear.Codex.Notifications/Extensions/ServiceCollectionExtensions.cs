#region

using System.Reflection;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Notifications.Services;
using MessagePipe;
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
        services.TryAddSingleton<IResultDiagnostics, DefaultResultDiagnostics>();

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
}
