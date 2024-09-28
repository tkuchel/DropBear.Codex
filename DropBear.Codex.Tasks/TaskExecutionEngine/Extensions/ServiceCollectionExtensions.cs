#region

using System.Reflection;
using DropBear.Codex.Tasks.TaskExecutionEngine.Filters;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Extensions;

/// <summary>
///     Provides extension methods for registering the task execution engine and related services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the task execution engine and its dependencies to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTaskExecutionEngine(this IServiceCollection services)
    {
        // Add ExecutionOptions configuration if not already configured
        services.AddOptions<ExecutionOptions>();

        // Register the ExecutionEngineFactory
        services.AddSingleton<IExecutionEngineFactory, ExecutionEngineFactory>();

        // Register the ExecutionEngine with Scoped lifetime for Blazor Server apps
        services.AddTransient<ExecutionEngine>();

        // Add your custom logging filter to the DI container
        services.AddScoped(typeof(AsyncMessageHandlerFilter<>), typeof(AsyncLoggingFilter<>));
        services.AddScoped(typeof(AsyncMessageHandlerFilter<>), typeof(AsyncExceptionHandlingFilter<>));
        services.AddScoped(typeof(AsyncMessageHandlerFilter<>), typeof(AsyncPerformanceMonitoringFilter<>));
        services.AddScoped(typeof(AsyncMessageHandlerFilter<>),
            serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, typeof(AsyncThrottlingFilter<>), 5));
        services.AddScoped(typeof(AsyncMessageHandlerFilter<>),
            serviceProvider =>
                ActivatorUtilities.CreateInstance(serviceProvider, typeof(AsyncRetryFilter<>), 3,
                    TimeSpan.FromSeconds(1)));



        // Register MessagePipe with the desired configuration
        services.AddMessagePipe(options =>
        {
            // Configure MessagePipe options as needed
            options.EnableCaptureStackTrace = false;
            options.EnableAutoRegistration = true;
            options.AddGlobalAsyncMessageHandlerFilter(typeof(AsyncLoggingFilter<>));
            options.AddGlobalAsyncMessageHandlerFilter(typeof(AsyncExceptionHandlingFilter<>));
            options.AddGlobalAsyncMessageHandlerFilter(typeof(AsyncPerformanceMonitoringFilter<>));
            options.AddGlobalAsyncMessageHandlerFilter(typeof(AsyncThrottlingFilter<>));
            options.SetAutoRegistrationSearchAssemblies(Assembly.GetExecutingAssembly());
        });

        return services;
    }
}
