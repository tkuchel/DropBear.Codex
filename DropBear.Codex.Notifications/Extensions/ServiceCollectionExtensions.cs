#region

using System.Reflection;
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

        ConfigureMessagePipe(services);
        AddNotificationServices(services, configuration, batchSize);

        if (enableSerialization)
        {
            if (enableEncryption)
            {
                AddSerializationServicesWithEncryption(services, configuration);
            }
            else
            {
                AddSerializationServices(services);
            }
        }

        AddGlobalNotificationService(services, enableSerialization);

        return services;
    }

    private static void ConfigureMessagePipe(IServiceCollection services)
    {
        services.AddMessagePipe(options =>
        {
            options.SetAutoRegistrationSearchAssemblies(Assembly.GetExecutingAssembly());
        });
    }

    private static void AddNotificationServices(IServiceCollection services, IConfiguration configuration,
        int batchSize)
    {
        services.AddSingleton<UserBufferedPublisher<byte[]>>();

        services.AddSingleton<NotificationBatchService>(provider =>
        {
            var batchPublisher = provider.GetRequiredService<IAsyncPublisher<List<byte[]>>>();
            return new NotificationBatchService(batchPublisher, batchSize);
        });

        services.AddSingleton<NotificationPersistenceService>(provider =>
        {
            var filePath = configuration["Notifications:AuditLogFilePath"]
                           ?? "backup_notifications_audit_log.json";
            return new NotificationPersistenceService(filePath);
        });
    }

    private static void AddSerializationServices(IServiceCollection services)
    {
        services.AddSingleton<ISerializer>(provider => new SerializationBuilder()
            .WithEncoding<Base64EncodingProvider>()
            .WithDefaultJsonSerializerOptions()
            .Build());
    }

    private static void AddSerializationServicesWithEncryption(IServiceCollection services,
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

    private static void AddGlobalNotificationService(IServiceCollection services, bool enableSerialization)
    {
        services.AddSingleton<GlobalNotificationService>(provider =>
        {
            var publisher = provider.GetRequiredService<IAsyncPublisher<string, byte[]>>();
            var userBufferedPublisher = provider.GetRequiredService<UserBufferedPublisher<byte[]>>();
            var persistenceService = provider.GetRequiredService<NotificationPersistenceService>();
            var batchService = provider.GetRequiredService<NotificationBatchService>();
            var serializer = enableSerialization ? provider.GetRequiredService<ISerializer>() : null;

            return new GlobalNotificationService(publisher, userBufferedPublisher, persistenceService, batchService,
                serializer);
        });
    }
}
