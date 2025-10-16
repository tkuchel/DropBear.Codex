#region

using System.Reflection;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Configuration;
using DropBear.Codex.Workflow.Persistence.Implementation;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Repositories;
using DropBear.Codex.Workflow.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#endregion

namespace DropBear.Codex.Workflow.Extensions;

/// <summary>
///     Extension methods for registering workflow services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers core workflow services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWorkflowEngine, WorkflowEngine>();

        return services;
    }

    /// <summary>
    ///     Registers a workflow definition.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow definition type</typeparam>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">The service lifetime (default: Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflow<TWorkflow, TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TWorkflow : class, IWorkflowDefinition<TContext>
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(IWorkflowDefinition<TContext>), typeof(TWorkflow), lifetime));
        services.Add(new ServiceDescriptor(typeof(TWorkflow), typeof(TWorkflow), lifetime));

        return services;
    }

    /// <summary>
    ///     Registers a workflow step.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">The service lifetime (default: Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowStep<TStep, TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TStep : class, IWorkflowStep<TContext>
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(IWorkflowStep<TContext>), typeof(TStep), lifetime));
        services.Add(new ServiceDescriptor(typeof(TStep), typeof(TStep), lifetime));

        return services;
    }

    /// <summary>
    ///     Registers all workflow steps from an assembly.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assembly">The assembly to scan for workflow steps</param>
    /// <param name="lifetime">The service lifetime (default: Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowStepsFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        IEnumerable<Type> stepTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && IsWorkflowStep(t));

        foreach (Type stepType in stepTypes)
        {
            Type? stepInterface = stepType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>));

            if (stepInterface is not null)
            {
                services.Add(new ServiceDescriptor(stepInterface, stepType, lifetime));
                services.Add(new ServiceDescriptor(stepType, stepType, lifetime));
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers persistent workflow services including state repository and notification service.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure persistent workflow options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPersistentWorkflow(
        this IServiceCollection services,
        Action<PersistentWorkflowOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure base workflow engine is registered
        services.AddWorkflowEngine();

        // Register persistent workflow engine
        services.TryAddSingleton<IPersistentWorkflowEngine, PersistentWorkflowEngine>();

        // Register default in-memory state repository if not already registered
        services.TryAddSingleton<IWorkflowStateRepository, InMemoryWorkflowStateRepository>();

        // Register no-op notification service as default if not already registered
        services.TryAddSingleton<IWorkflowNotificationService, NoOpWorkflowNotificationService>();

        // Configure options
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        // Register background services if enabled in options
        services.AddHostedService<WorkflowTimeoutService>();

        return services;
    }

    /// <summary>
    ///     Registers workflow dependencies from the calling assembly.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">The service lifetime (default: Scoped)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowsFromAssembly(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);

        var callingAssembly = Assembly.GetCallingAssembly();
        return services.AddWorkflowStepsFromAssembly(callingAssembly, lifetime);
    }

    private static bool IsWorkflowStep(Type type)
    {
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>));
    }
}
