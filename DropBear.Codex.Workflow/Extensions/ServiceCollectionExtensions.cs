#region

using System.Reflection;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Implementation;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Workflow.Extensions;

/// <summary>
///     Extension methods for registering persistent workflow services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers core workflow services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register core workflow services
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        // Add logging if not already registered
        // services.AddLogging();

        return services;
    }

    /// <summary>
    ///     Registers a workflow definition with the dependency injection container.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow type</typeparam>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The service collection for method chaining</returns>
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
    ///     Registers a workflow step with the dependency injection container.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The service collection for method chaining</returns>
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
    ///     Registers multiple workflow steps from an assembly.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assembly">Assembly to scan for workflow steps</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddWorkflowStepsFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var stepTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>)))
            .ToList();

        foreach (var stepType in stepTypes)
        {
            var interfaces = stepType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>));

            foreach (var @interface in interfaces)
            {
                services.Add(new ServiceDescriptor(@interface, stepType, lifetime));
                services.Add(new ServiceDescriptor(stepType, stepType, lifetime));
            }
        }

        return services;
    }

    /// <summary>
    ///     Adds workflow execution options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure workflow options</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection ConfigureWorkflowOptions(
        this IServiceCollection services,
        Action<WorkflowExecutionOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new WorkflowExecutionOptions();
        configureOptions(options);

        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    ///     Registers persistent workflow services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddPersistentWorkflowEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the persistent workflow engine
        services.AddSingleton<IPersistentWorkflowEngine, PersistentWorkflowEngine>();

        // Register background services
        services.AddHostedService<WorkflowTimeoutService>();

        return services;
    }

    /// <summary>
    ///     Registers a workflow state repository implementation
    /// </summary>
    /// <typeparam name="TRepository">The repository implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddWorkflowStateRepository<TRepository>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRepository : class, IWorkflowStateRepository
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(IWorkflowStateRepository), typeof(TRepository), lifetime));

        return services;
    }

    /// <summary>
    ///     Registers a workflow notification service implementation
    /// </summary>
    /// <typeparam name="TNotificationService">The notification service implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddWorkflowNotificationService<TNotificationService>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TNotificationService : class, IWorkflowNotificationService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(
            new ServiceDescriptor(typeof(IWorkflowNotificationService), typeof(TNotificationService), lifetime));

        return services;
    }
}
