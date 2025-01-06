#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Factories;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ServiceCollectionExtensions));

    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddSerializationServices(this IServiceCollection services,
        Action<SerializationBuilder> configure)
    {
        Logger.Information("Adding serialization services.");

        if (configure is null)
        {
            var errorMessage = "The configuration action for SerializationBuilder cannot be null.";
            Logger.Error(errorMessage);
            throw new ArgumentNullException(nameof(configure), errorMessage);
        }

        var builder = new SerializationBuilder();
        configure(builder);

        try
        {
            var serializer = builder.Build();
            services.AddSingleton(serializer);
            Logger.Information("Serialization services successfully added.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to build and register the serializer.");
            throw;
        }

        return services;
    }
}
