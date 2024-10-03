#region

using System.Reflection;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Notifications.Services;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Providers;
using MessagePipe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Notifications.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to configure notification services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds notification services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The configuration containing necessary settings.</param>
    /// <param name="batchSize">The size of notification batches. Default is 10.</param>
    /// <param name="enableSerialization">Whether to enable serialization. Default is false.</param>
    /// <param name="enableEncryption">Whether to enable encryption for serialization. Default is false.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        IConfiguration configuration,
        int batchSize = 10,
        bool enableSerialization = false,
        bool enableEncryption = false)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
        }

        // Configure core services
        services.ConfigureMessagePipe();
        services.AddNotificationBatching(configuration, batchSize);

        // Configure serialization, if enabled
        if (enableSerialization)
        {
            services.AddSerializationServices(configuration, enableEncryption);
        }

        // Register notification services
        services.AddScoped<GlobalNotificationService>();
        services.AddScoped<INotificationSerializationService, NotificationSerializationService>();

        return services;
    }

    private static void ConfigureMessagePipe(this IServiceCollection services)
    {
        services.AddMessagePipe(options =>
        {
            options.InstanceLifetime = InstanceLifetime.Scoped;
            options.SetAutoRegistrationSearchAssemblies(Assembly.GetExecutingAssembly());
        });
    }

    private static void AddNotificationBatching(this IServiceCollection services, IConfiguration configuration,
        int batchSize)
    {
        services.AddScoped<UserBufferedPublisher<byte[]>>();

        services.AddScoped<NotificationBatchService>(provider =>
        {
            var batchPublisher = provider.GetRequiredService<IAsyncPublisher<List<byte[]>>>();
            return new NotificationBatchService(batchPublisher, batchSize);
        });

        services.AddScoped<NotificationPersistenceService>(provider =>
        {
            var filePath = configuration["Notifications:AuditLogFilePath"] ?? "backup_notifications_audit_log.json";
            return new NotificationPersistenceService(filePath);
        });
    }

    private static void AddSerializationServices(this IServiceCollection services, IConfiguration configuration,
        bool enableEncryption)
    {
        if (enableEncryption)
        {
            services.AddEncryptedSerializationServices(configuration);
        }
        else
        {
            services.AddBasicSerializationServices();
        }
    }

    private static void AddBasicSerializationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISerializer>(provider => new SerializationBuilder()
            .WithEncoding<Base64EncodingProvider>()
            .WithDefaultJsonSerializerOptions()
            .Build());
    }

    private static void AddEncryptedSerializationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var publicKeyPath = configuration["Encryption:PublicKeyPath"];
        var privateKeyPath = configuration["Encryption:PrivateKeyPath"];

        if (string.IsNullOrEmpty(publicKeyPath) || string.IsNullOrEmpty(privateKeyPath))
        {
            throw new InvalidOperationException("Encryption keys are not properly configured.");
        }

        services.AddSingleton<ISerializer>(provider => new SerializationBuilder()
            .WithEncoding<Base64EncodingProvider>()
            .WithEncryption<AESCNGEncryptionProvider>()
            .WithKeys(publicKeyPath, privateKeyPath)
            .WithDefaultJsonSerializerOptions()
            .Build());
    }
}
