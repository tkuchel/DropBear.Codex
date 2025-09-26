using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Extension methods for registering DropBear Blazor services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds DropBear snackbar services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDropBearSnackbar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the snackbar service as a singleton for Blazor Server
        // (use Scoped for Blazor WebAssembly)
        services.TryAddSingleton<ISnackbarService, SnackbarService>();

        return services;
    }

    /// <summary>
    ///     Adds DropBear snackbar services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for snackbar options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDropBearSnackbar(
        this IServiceCollection services,
        Action<SnackbarOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddDropBearSnackbar();
        services.Configure(configure);

        return services;
    }
}
