#region

using System.Reflection;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
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

        // Register the ExecutionEngine with Scoped or Transient lifetime
        // *** CHANGE *** The original code used Transient.
        // You can keep Transient if each consumer wants a fresh engine.
        services.AddTransient<ExecutionEngine>();

        // Register message pipe if needed
        services.AddMessagePipe(options =>
        {
            options.EnableCaptureStackTrace = true;
            options.EnableAutoRegistration = true;
            options.SetAutoRegistrationSearchAssemblies(Assembly.GetExecutingAssembly());
        });

        return services;
    }
}
