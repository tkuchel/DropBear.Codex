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
    private static readonly ILogger _logger = LoggerFactory.Logger.ForContext(typeof(ServiceCollectionExtensions));

    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddSerializationServices(this IServiceCollection services,
        Action<SerializationBuilder> configure)
    {
        _logger.Information("Adding serialization services.");

        if (configure is null)
        {
            var errorMessage = "The configuration action for SerializationBuilder cannot be null.";
            _logger.Error(errorMessage);
            throw new ArgumentNullException(nameof(configure), errorMessage);
        }

        var builder = new SerializationBuilder();
        configure(builder);

        try
        {
            var serializer = builder.Build();
            services.AddSingleton(serializer);
            _logger.Information("Serialization services successfully added.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to build and register the serializer.");
            throw;
        }

        return services;
    }
}
