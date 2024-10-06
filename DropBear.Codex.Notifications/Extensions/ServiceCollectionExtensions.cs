#region

using System.Reflection;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Services;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="configureMessagePipe">Optional configuration for MessagePipe options.</param>
    /// <returns>The IServiceCollection for chaining additional operations.</returns>
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        Action<MessagePipeOptions>? configureMessagePipe = null)
    {
        // Configure MessagePipe
        services.ConfigureMessagePipe(configureMessagePipe);

        // Register the NotificationService using its interface
        services.AddSingleton<INotificationService, NotificationService>();

        // Optionally register the NotificationFactory if used
        services.AddSingleton<INotificationFactory, NotificationFactory>();

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
