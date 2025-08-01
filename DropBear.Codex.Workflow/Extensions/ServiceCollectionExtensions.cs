using Microsoft.Extensions.DependencyInjection;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Implementation;
using DropBear.Codex.Workflow.Persistence.Services;

namespace DropBear.Codex.Workflow.Persistence.Extensions;

/// <summary>
/// Extension methods for registering persistent workflow services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistent workflow services with the dependency injection container
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
    /// Registers a workflow state repository implementation
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
    /// Registers a workflow notification service implementation
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

        services.Add(new ServiceDescriptor(typeof(IWorkflowNotificationService), typeof(TNotificationService), lifetime));
        
        return services;
    }
}
