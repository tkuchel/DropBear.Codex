#region

using System.Reflection;
using DropBear.Codex.Notifications.Models;
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
    ///     Configures and registers the NotificationService along with required dependencies.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The IServiceCollection for chaining additional operations.</returns>
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        // Ensure that MessagePipe or any similar message-passing framework is configured.
        services.AddMessagePipe();

        // Register the IAsyncPublisher<string, Notification> dependency (MessagePipe takes care of this).
        services.AddSingleton<IAsyncPublisher<string, Notification>>(provider =>
        {
            var publisher = provider.GetRequiredService<IAsyncPublisher<string, Notification>>();
            return publisher;
        });

        // Register the NotificationService
        services.AddScoped<NotificationService>();

        return services;
    }

    /// <summary>
    ///     Configures MessagePipe with default options.
    /// </summary>
    /// <param name="services">The IServiceCollection.</param>
    /// <returns>The IServiceCollection for chaining additional operations.</returns>
    private static IServiceCollection AddMessagePipe(this IServiceCollection services)
    {
        services.AddMessagePipe(options =>
        {
            // Configure MessagePipe options, such as assembly scanning or logging
            options.SetAutoRegistrationSearchAssemblies(Assembly.GetExecutingAssembly());
            options.EnableCaptureStackTrace = true;
        });

        return services;
    }
}
