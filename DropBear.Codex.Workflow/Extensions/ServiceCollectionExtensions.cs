#region

using System.Reflection;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Implementation;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

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
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        return services;
    }

    /// <summary>
    ///     Registers a workflow definition.
    /// </summary>
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
            Type? contextType = GetWorkflowContextType(stepType);
            if (contextType is null)
            {
                continue;
            }

            Type stepInterfaceType = typeof(IWorkflowStep<>).MakeGenericType(contextType);
            services.Add(new ServiceDescriptor(stepInterfaceType, stepType, lifetime));
            services.Add(new ServiceDescriptor(stepType, stepType, lifetime));
        }

        return services;
    }

    /// <summary>
    ///     Registers persistent workflow services.
    /// </summary>
    public static IServiceCollection AddPersistentWorkflow<TContext>(
        this IServiceCollection services,
        Action<PersistentWorkflowOptions>? configure = null)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowEngine();
        services.AddSingleton<IPersistentWorkflowEngine, PersistentWorkflowEngine>();

        var options = new PersistentWorkflowOptions();
        configure?.Invoke(options);

        if (options.EnableTimeoutProcessing)
        {
            services.AddHostedService<WorkflowTimeoutService>();
        }

        return services;
    }

    private static bool IsWorkflowStep(Type type)
    {
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>));
    }

    private static Type? GetWorkflowContextType(Type stepType)
    {
        Type? stepInterface = stepType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>));

        return stepInterface?.GetGenericArguments().FirstOrDefault();
    }
}
